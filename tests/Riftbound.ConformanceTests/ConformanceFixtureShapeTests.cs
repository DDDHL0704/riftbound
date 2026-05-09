using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ConformanceFixtureShapeTests
{
    [Fact]
    public async Task DuplicateClientIntentDoesNotAdvanceTickTwice()
    {
        var journal = new RecordingMatchJournal();
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        await ReadyBothAsync(session);

        var first = await session.SubmitAsync("P1", "intent-1", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        var duplicate = await session.SubmitAsync("P1", "intent-1", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        var gameplayEntries = journal.Entries
            .Where(entry => string.Equals(entry.CommandType, "PASS", StringComparison.Ordinal))
            .ToArray();

        Assert.True(first.Accepted);
        Assert.True(duplicate.Accepted);
        Assert.Equal(first.State.Tick, duplicate.State.Tick);
        Assert.Equal(first.Events, duplicate.Events);
        Assert.Single(gameplayEntries);
    }

    [Fact]
    public async Task JournalEntriesCarryMonotonicEventSequenceBounds()
    {
        var journal = new RecordingMatchJournal();
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        await ReadyBothAsync(session);

        await session.SubmitAsync("P1", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        await session.SubmitAsync("P1", "intent-end-turn", new EndTurnCommand(), RawCommand("END_TURN"), CancellationToken.None);
        await session.SubmitAsync("P1", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None);
        var gameplayEntries = journal.Entries
            .Where(entry => !string.Equals(entry.CommandType, "READY", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, gameplayEntries.Length);
        Assert.Equal(3, gameplayEntries[0].StartedEventSequence);
        Assert.Equal(4, gameplayEntries[0].CompletedEventSequence);
        Assert.Equal(4, gameplayEntries[1].StartedEventSequence);
        Assert.Equal(9, gameplayEntries[1].CompletedEventSequence);
    }

    [Fact]
    public async Task JournalEntryKeepsOriginalCommandPayload()
    {
        var journal = new RecordingMatchJournal();
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        await ReadyBothAsync(session);
        var raw = JsonDocument.Parse("""{"cmdType":"PASS","clientNote":"keep-me"}""").RootElement.Clone();

        await session.SubmitAsync("P1", "intent-pass", new PassCommand(), raw, CancellationToken.None);

        var entry = Assert.Single(journal.Entries, entry =>
            string.Equals(entry.CommandType, "PASS", StringComparison.Ordinal));
        Assert.NotNull(entry.RawCommand);
        Assert.Equal("keep-me", entry.RawCommand.Value.GetProperty("clientNote").GetString());
    }

    [Fact]
    public async Task SubmitRequiresPlayerToJoinRoomFirst()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.SubmitAsync("alice", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None));

        Assert.Equal(ErrorCodes.PlayerNotInRoom, error.Code);
        Assert.Equal("玩家不在房间中。", error.Message);
        Assert.DoesNotContain("player is not in room", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsurePlayerRequiresPlayerId()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());

        var error = Assert.Throws<MatchSessionException>(() => session.EnsurePlayer(" "));

        Assert.Equal(ErrorCodes.PlayerIdRequired, error.Code);
        Assert.Equal("玩家编号不能为空。", error.Message);
        Assert.DoesNotContain("playerId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitRequiresMatchToStart()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());
        session.EnsurePlayer("alice");
        session.EnsurePlayer("bob");

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.SubmitAsync("alice", "intent-pass", new PassCommand(), RawCommand("PASS"), CancellationToken.None));

        Assert.Equal(ErrorCodes.MatchNotStarted, error.Code);
        Assert.Equal("对局尚未开始。", error.Message);
        Assert.DoesNotContain("match has not started", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitRequiresClientIntentId()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        await ReadyBothAsync(session);

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.SubmitAsync("P1", " ", new PassCommand(), RawCommand("PASS"), CancellationToken.None));

        Assert.Equal(ErrorCodes.ClientIntentIdRequired, error.Code);
        Assert.Equal("客户端行动编号不能为空。", error.Message);
        Assert.DoesNotContain("clientIntentId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadyRequiresClientIntentId()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());
        session.EnsurePlayer("P1");

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.ReadyAsync("P1", "", RawCommand("READY"), CancellationToken.None));

        Assert.Equal(ErrorCodes.ClientIntentIdRequired, error.Code);
        Assert.Equal("客户端行动编号不能为空。", error.Message);
        Assert.DoesNotContain("clientIntentId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeedScenarioRequiresScenarioId()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.SeedScenarioAsync("P1", "seed-missing-scenario", " ", RawCommand("DEV_SEED_SCENARIO"), CancellationToken.None));

        Assert.Equal(ErrorCodes.UnsupportedCommand, error.Code);
        Assert.Equal("开发测试场景编号不能为空。", error.Message);
        Assert.DoesNotContain("scenarioId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeedScenarioRequiresTwoJoinedPlayers()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());
        session.EnsurePlayer("P1");

        var error = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await session.SeedScenarioAsync("P1", "seed-one-player", "basic-play", RawCommand("DEV_SEED_SCENARIO"), CancellationToken.None));

        Assert.Equal(ErrorCodes.PlayerNotInRoom, error.Code);
        Assert.Equal("开发测试场景需要两名玩家都已加入房间。", error.Message);
        Assert.DoesNotContain("dev scenarios", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OfficialOnlyRoomsRejectReadyBeforeDeckSubmission()
    {
        var session = new MatchSession(
            "official-only-room",
            new PlaceholderRuleEngine(),
            NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance,
            new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false));
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var result = await session.ReadyAsync(
            "P1",
            "ready-without-deck",
            RawCommand("READY"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, result.ErrorCode);
        Assert.Equal("正式卡组房间需要先提交合法卡组才能准备。", result.ErrorMessage);
        Assert.DoesNotContain("valid deck", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("P1", result.State.ReadyPlayerIds);
        Assert.Equal(MatchStatuses.Seating, result.State.Status);
    }

    [Fact]
    public void JoinAssignsStableP1P2SeatsAndSnapshotsExposeSeatStatus()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());

        session.EnsurePlayer("alice");
        session.EnsurePlayer("bob");
        session.EnsurePlayer("alice");

        var aliceSnapshot = session.SnapshotFor("alice");
        var bobSnapshot = session.SnapshotFor("bob");

        Assert.Equal("alice", aliceSnapshot.ActivePlayerId);
        Assert.Equal("alice", bobSnapshot.ActivePlayerId);
        Assert.Equal("P1", PlayerSeat(aliceSnapshot, "alice"));
        Assert.Equal("P2", PlayerSeat(aliceSnapshot, "bob"));
        Assert.Equal("P1", PlayerSeat(bobSnapshot, "alice"));
        Assert.Equal("P2", PlayerSeat(bobSnapshot, "bob"));
    }

    [Fact]
    public async Task MatchStateCarriesP2AuthorityFields()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());
        session.EnsurePlayer("alice");
        session.EnsurePlayer("bob");
        await ReadyBothAsync(session, "alice", "bob");

        var snapshot = session.SnapshotFor("alice");
        var timing = snapshot.Timing;

        Assert.Equal("MAIN", timing["phase"]);
        Assert.Equal("NEUTRAL_OPEN", timing["timingState"]);
        Assert.Equal("alice", timing["turnPlayerId"]);
        Assert.Equal("IN_PROGRESS", timing["roomStatus"]);
    }

    [Fact]
    public void SnapshotsDoNotExposeRandomSeedOrCursor()
    {
        var state = new MatchState(
            "privacy-room",
            9,
            2,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            seed: 260330,
            rngCursor: 7);

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];

        Assert.DoesNotContain("seed", snapshot.Timing.Keys);
        Assert.DoesNotContain("rngCursor", snapshot.Timing.Keys);
    }

    [Fact]
    public void JoinRejectsThirdPlayer()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());

        session.EnsurePlayer("alice");
        session.EnsurePlayer("bob");

        var error = Assert.Throws<MatchSessionException>(() => session.EnsurePlayer("charlie"));
        Assert.Equal(ErrorCodes.RoomFull, error.Code);
        Assert.Equal("房间已有两名玩家。", error.Message);
        Assert.DoesNotContain("room already has two players", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReconnectTokenIsStableAndRequired()
    {
        var session = new MatchSession("fixture-room", new PlaceholderRuleEngine());

        var join = session.EnsurePlayer("alice");
        var duplicateJoin = session.EnsurePlayer(" alice ");
        var reconnect = session.ReconnectPlayer("alice", join.ReconnectToken);

        Assert.Equal(join, duplicateJoin);
        Assert.Equal(join, reconnect);
        Assert.Throws<MatchSessionException>(() => session.ReconnectPlayer("alice", "bad-token"));
    }

    [Fact]
    public void ProtocolEnvelopeKeepsCurrentContractFields()
    {
        var message = new WsServerMessage(
            MessageType.SNAPSHOT,
            "room",
            "P1",
            7,
            new { tick = 7 });

        Assert.Equal(MessageType.SNAPSHOT, message.Type);
        Assert.Equal("room", message.RoomId);
        Assert.Equal("P1", message.PlayerId);
        Assert.Equal(7, message.ServerTick);
        Assert.Equal(ProtocolDefaults.ProtocolVersion, message.ProtocolVersion);
        Assert.Equal(ProtocolDefaults.SchemaVersion, message.SchemaVersion);
    }

    [Fact]
    public void ClientEnvelopeDefaultsProtocolVersions()
    {
        var cmd = JsonDocument.Parse("""{"cmdType":"READY"}""").RootElement.Clone();

        var message = new WsClientMessage(MessageType.READY, "room", "P1", "intent-ready", Cmd: cmd);

        Assert.Equal(MessageType.READY, message.Type);
        Assert.Equal("room", message.RoomId);
        Assert.Equal("P1", message.PlayerId);
        Assert.Equal("intent-ready", message.ClientIntentId);
        Assert.Equal(ProtocolDefaults.ProtocolVersion, message.ProtocolVersion);
        Assert.Equal(ProtocolDefaults.SchemaVersion, message.SchemaVersion);
    }

    [Theory]
    [InlineData("READY", typeof(ReadyCommand))]
    [InlineData("PASS_PRIORITY", typeof(PassPriorityCommand))]
    [InlineData("PASS_FOCUS", typeof(PassFocusCommand))]
    [InlineData("PASS", typeof(PassCommand))]
    [InlineData("END_TURN", typeof(EndTurnCommand))]
    public void GameCommandMapperKeepsPassAndEndTurnSemanticsDistinct(string cmdType, Type expectedType)
    {
        var command = GameCommandJsonMapper.Map(JsonDocument.Parse($$"""{"cmdType":"{{cmdType}}"}""").RootElement);

        Assert.IsType(expectedType, command);
        Assert.Equal(cmdType, command.CmdType);
    }

    [Fact]
    public void GameCommandMapperParsesPlayCardPayload()
    {
        var command = Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-SPELL-PUNISHMENT",
              "cardNo": "UNL-007/219",
              "targetObjectIds": ["P2-UNIT-001"],
              "mode": "BASE_UNIT_DAMAGE_4",
              "optionalCosts": ["ECHO"]
            }
            """).RootElement));

        Assert.Equal("P1-SPELL-PUNISHMENT", command.SourceObjectId);
        Assert.Equal("UNL-007/219", command.CardNo);
        Assert.Equal(new[] { "P2-UNIT-001" }, command.TargetObjectIds);
        Assert.Equal("BASE_UNIT_DAMAGE_4", command.Mode);
        Assert.Equal(new[] { "ECHO" }, command.OptionalCosts);
        Assert.Equal(string.Empty, command.Destination);
    }

    [Fact]
    public void GameCommandMapperParsesAmbushPlayCardDestination()
    {
        var command = Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "PLAY_CARD",
              "sourceObjectId": "P1-HAND-UNL-GLOOMY-APOTHECARY",
              "cardNo": "UNL-021/219",
              "targetObjectIds": [],
              "mode": "AMBUSH",
              "destination": "BATTLEFIELD:P1-MAIN"
            }
            """).RootElement));

        Assert.Equal("P1-HAND-UNL-GLOOMY-APOTHECARY", command.SourceObjectId);
        Assert.Equal("UNL-021/219", command.CardNo);
        Assert.Empty(command.TargetObjectIds);
        Assert.Equal("AMBUSH", command.Mode);
        Assert.Empty(command.OptionalCosts ?? []);
        Assert.Equal("BATTLEFIELD:P1-MAIN", command.Destination);
    }

    [Fact]
    public void GameCommandMapperParsesActivateAbilityPayload()
    {
        var command = Assert.IsType<ActivateAbilityCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "ACTIVATE_ABILITY",
              "sourceObjectId": "P1-UNIT-VI",
              "abilityId": "PAY_2_RED_DOUBLE_POWER",
              "targetObjectIds": ["P2-SPELLSHIELD-UNIT-001"],
              "optionalCosts": ["SPEND_EXPERIENCE:1"]
            }
            """).RootElement));

        Assert.Equal("P1-UNIT-VI", command.SourceObjectId);
        Assert.Equal("PAY_2_RED_DOUBLE_POWER", command.AbilityId);
        Assert.Equal(new[] { "P2-SPELLSHIELD-UNIT-001" }, command.TargetObjectIds);
        Assert.Equal(new[] { "SPEND_EXPERIENCE:1" }, command.OptionalCosts);
    }

    [Fact]
    public void GameCommandMapperParsesHideCardPayload()
    {
        var command = Assert.IsType<HideCardCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "HIDE_CARD",
              "sourceObjectId": "P1-HAND-OGN-TEEMO",
              "cardNo": "OGN·121/298",
              "destination": "STANDBY",
              "optionalCosts": ["STANDBY_A"]
            }
            """).RootElement));

        Assert.Equal("P1-HAND-OGN-TEEMO", command.SourceObjectId);
        Assert.Equal("OGN·121/298", command.CardNo);
        Assert.Equal("STANDBY", command.Destination);
        Assert.Equal(new[] { "STANDBY_A" }, command.OptionalCosts);
    }

    [Fact]
    public void GameCommandMapperParsesRevealCardPayload()
    {
        var command = Assert.IsType<RevealCardCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "REVEAL_CARD",
              "sourceObjectId": "P1-FACEDOWN-OGN-TEEMO",
              "cardNo": "OGN·121/298",
              "targetObjectIds": ["P2-BATTLEFIELD-UNIT-001"],
              "mode": "STANDBY_REACTION",
              "optionalCosts": ["STANDBY_REVEAL_0"],
              "destination": "STACK"
            }
            """).RootElement));

        Assert.Equal("P1-FACEDOWN-OGN-TEEMO", command.SourceObjectId);
        Assert.Equal("OGN·121/298", command.CardNo);
        Assert.Equal(new[] { "P2-BATTLEFIELD-UNIT-001" }, command.TargetObjectIds);
        Assert.Equal("STANDBY_REACTION", command.Mode);
        Assert.Equal(new[] { "STANDBY_REVEAL_0" }, command.OptionalCosts);
        Assert.Equal("STACK", command.Destination);
    }

    [Fact]
    public void GameCommandMapperParsesMoveUnitPayload()
    {
        var command = Assert.IsType<MoveUnitCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "MOVE_UNIT",
              "sourceObjectId": "P1-BATTLEFIELD-SFD-YASUO",
              "origin": "BATTLEFIELD:P1-LEFT",
              "destination": "BATTLEFIELD:P1-RIGHT",
              "optionalCosts": ["ROAM"]
            }
            """).RootElement));

        Assert.Equal("P1-BATTLEFIELD-SFD-YASUO", command.SourceObjectId);
        Assert.Equal("BATTLEFIELD:P1-LEFT", command.Origin);
        Assert.Equal("BATTLEFIELD:P1-RIGHT", command.Destination);
        Assert.Equal(new[] { "ROAM" }, command.OptionalCosts);
    }

    [Fact]
    public void GameCommandMapperParsesAssembleEquipmentPayload()
    {
        var command = Assert.IsType<AssembleEquipmentCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "ASSEMBLE_EQUIPMENT",
              "sourceObjectId": "P1-EQUIPMENT-LONG-SWORD",
              "targetObjectId": "P1-UNIT-ASSEMBLE-TARGET",
              "optionalCosts": ["ASSEMBLE_RED"]
            }
            """).RootElement));

        Assert.Equal("P1-EQUIPMENT-LONG-SWORD", command.SourceObjectId);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", command.TargetObjectId);
        Assert.Equal(new[] { "ASSEMBLE_RED" }, command.OptionalCosts);
    }

    [Fact]
    public void GameCommandMapperParsesDeclareBattlePayload()
    {
        var command = Assert.IsType<DeclareBattleCommand>(GameCommandJsonMapper.Map(JsonDocument.Parse("""
            {
              "cmdType": "DECLARE_BATTLE",
              "battlefieldId": "BATTLEFIELD:P1-MAIN",
              "attackerObjectIds": ["P1-BATTLEFIELD-GAREN"],
              "defenderObjectIds": ["P2-BATTLEFIELD-MUTANT-KITTEN"],
              "optionalCosts": ["COMBAT_ASSIGNMENT"]
            }
            """).RootElement));

        Assert.Equal("BATTLEFIELD:P1-MAIN", command.BattlefieldId);
        Assert.Equal(new[] { "P1-BATTLEFIELD-GAREN" }, command.AttackerObjectIds);
        Assert.Equal(new[] { "P2-BATTLEFIELD-MUTANT-KITTEN" }, command.DefenderObjectIds);
        Assert.Equal(new[] { "COMBAT_ASSIGNMENT" }, command.OptionalCosts);
    }

    [Fact]
    public void ActionPromptDeclareBattleMetadataFiltersSourcesDefendersBattlefieldsAndCosts()
    {
        var state = new MatchState(
            "prompt-declare-battle-room",
            15,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BASE-UNIT"],
                    Battlefields = ["P1-ATTACKER", "P1-FACEDOWN", "P1-ALREADY-ATTACKING", "P1-OPPONENT-CONTROLLED-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-CARD", "P2-DEFENDER", "P2-BULWARK-DEFENDER", "P2-FACEDOWN", "P2-EQUIPMENT", "P2-ACTING-CONTROLLED-DEFENDER"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-UNIT"] = new(
                    "P1-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-ATTACKER"] = new(
                    "P1-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FACEDOWN"] = new(
                    "P1-FACEDOWN",
                    isFaceDown: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-ALREADY-ATTACKING"] = new(
                    "P1-ALREADY-ATTACKING",
                    isAttacking: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-OPPONENT-CONTROLLED-ATTACKER"] = new(
                    "P1-OPPONENT-CONTROLLED-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-CARD"] = new(
                    "P2-BATTLEFIELD-CARD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DEFENDER"] = new(
                    "P2-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BULWARK-DEFENDER"] = new(
                    "P2-BULWARK-DEFENDER",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardCombatKeywordNames.Bulwark],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-FACEDOWN"] = new(
                    "P2-FACEDOWN",
                    isFaceDown: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-EQUIPMENT"] = new(
                    "P2-EQUIPMENT",
                    cardNo: "SFD·011/221",
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-ACTING-CONTROLLED-DEFENDER"] = new(
                    "P2-ACTING-CONTROLLED-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P1")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var battleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

        Assert.True(battleCandidate.Enabled);
        Assert.Equal(["P1-ATTACKER"], (battleCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        Assert.Equal(["P2-DEFENDER", "P2-BULWARK-DEFENDER"], (battleCandidate.Targets ?? []).Select(choice => choice.Id).ToArray());
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "BATTLEFIELD:P1-MAIN", StringComparison.Ordinal));
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "P2-BATTLEFIELD-CARD", StringComparison.Ordinal));
        Assert.DoesNotContain(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "BATTLEFIELD:P2-MAIN", StringComparison.Ordinal));
        Assert.Equal(["COMBAT_ASSIGNMENT"], (battleCandidate.OptionalCosts ?? []).Select(choice => choice.Id).ToArray());

        var metadata = Assert.IsType<Dictionary<string, object?>>(battleCandidate.Metadata);
        Assert.Equal(2, Assert.IsType<int>(metadata["defenderCountMax"]));
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var sourceRequirement = Assert.Single(sourceRequirements);
        Assert.Equal("P1-ATTACKER", Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["minDefenderCount"]));
        Assert.Equal(2, Assert.IsType<int>(sourceRequirement["maxDefenderCount"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["targetChoicesByIndex"]);
        var firstDefenderChoices = targetChoicesByIndex["0"];
        Assert.Equal(["P2-DEFENDER", "P2-BULWARK-DEFENDER"], firstDefenderChoices.Select(choice => choice.Id).ToArray());
        var secondDefenderChoices = targetChoicesByIndex["1"];
        Assert.Equal(["P2-DEFENDER", "P2-BULWARK-DEFENDER"], secondDefenderChoices.Select(choice => choice.Id).ToArray());
        var battlefieldChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["battlefieldChoices"]);
        Assert.Contains(battlefieldChoices, choice => string.Equals(choice.Id, "BATTLEFIELD:P1-MAIN", StringComparison.Ordinal));
        Assert.Equal(
            ["COMBAT_ASSIGNMENT"],
            Assert.IsAssignableFrom<IEnumerable<string>>(sourceRequirement["requiredOptionalCosts"]).ToArray());
        Assert.True(Assert.IsType<bool>(sourceRequirement["composable"]));
    }

    [Fact]
    public void ActionPromptHidesDeclareBattleCombatantsWhenUnitHasNoCardNo()
    {
        var state = new MatchState(
            "prompt-declare-battle-unknown-source-room",
            25,
            6,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLE-UNKNOWN-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLE-UNKNOWN-DEFENDER"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-UNKNOWN-ATTACKER"] = new(
                    "P1-BATTLE-UNKNOWN-ATTACKER",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLE-UNKNOWN-DEFENDER"] = new(
                    "P2-BATTLE-UNKNOWN-DEFENDER",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var battleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

        Assert.False(battleCandidate.Enabled);
        Assert.Empty(battleCandidate.Sources ?? []);
        Assert.Empty(battleCandidate.Targets ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(battleCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
    }

    [Fact]
    public void ActionPromptHidesDeclareBattleBattlefieldDestinationWhenCardHasNoCardNo()
    {
        var state = new MatchState(
            "prompt-declare-battle-unknown-battlefield-room",
            25,
            6,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION",
                        "P1-BATTLE-KNOWN-ATTACKER"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLE-KNOWN-DEFENDER"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION"] = new(
                    "P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLE-KNOWN-ATTACKER"] = new(
                    "P1-BATTLE-KNOWN-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLE-KNOWN-DEFENDER"] = new(
                    "P2-BATTLE-KNOWN-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var battleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));

        Assert.True(battleCandidate.Enabled);
        Assert.Equal(["P1-BATTLE-KNOWN-ATTACKER"], (battleCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        Assert.Equal(["P2-BATTLE-KNOWN-DEFENDER"], (battleCandidate.Targets ?? []).Select(choice => choice.Id).ToArray());
        Assert.Contains(battleCandidate.Destinations ?? [], choice => string.Equals(choice.Id, "BATTLEFIELD:P1-MAIN", StringComparison.Ordinal));
        Assert.DoesNotContain(
            battleCandidate.Destinations ?? [],
            choice => string.Equals(choice.Id, "P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(battleCandidate.Metadata);
        var sourceRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
        var battlefieldChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["battlefieldChoices"]);
        Assert.DoesNotContain(
            battlefieldChoices,
            choice => string.Equals(choice.Id, "P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION", StringComparison.Ordinal));
    }

    [Fact]
    public void SnapshotsExposeDevUiZonesWithoutLeakingOpponentHand()
    {
        var state = new MatchState(
            "dev-room",
            7,
            2,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["alice"] = new(3, 1),
                ["bob"] = new(2, 0)
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = new(
                    MainDeck: ["A-DECK-1"],
                    RuneDeck: ["A-RUNE-1"],
                    Hand: ["A-HAND-1"],
                    Base: ["A-BASE-1"],
                    Battlefields: [],
                    Graveyard: [],
                    Banished: [],
                    LegendZone: [],
                    ChampionZone: []),
                ["bob"] = new(
                    MainDeck: ["B-DECK-1"],
                    RuneDeck: ["B-RUNE-1"],
                    Hand: ["B-HAND-1"],
                    Base: [],
                    Battlefields: ["B-FIELD-1"],
                    Graveyard: [],
                    Banished: [],
                    LegendZone: [],
                    ChampionZone: [])
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["A-HAND-1"] = new("A-HAND-1", power: 1, tags: ["CARD_TYPE:UNIT"]),
                ["A-BASE-1"] = new("A-BASE-1", power: 2, tags: ["CARD_TYPE:UNIT"]),
                ["B-HAND-1"] = new("B-HAND-1", power: 3, tags: ["CARD_TYPE:UNIT"]),
                ["B-FIELD-1"] = new("B-FIELD-1", power: 4, tags: ["CARD_TYPE:UNIT"])
            });

        var aliceSnapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var aliceView = PlayerView(aliceSnapshot, "alice");
        var bobView = PlayerView(aliceSnapshot, "bob");
        var aliceZones = ZoneView(aliceView);
        var bobZones = ZoneView(bobView);
        var bobObjects = ObjectView(bobView);
        var lanes = Assert.IsType<Dictionary<string, object?>>(aliceSnapshot.Lanes);

        Assert.Equal("P1", Assert.IsType<string>(aliceView["seat"]));
        Assert.Contains("A-HAND-1", StringList(aliceZones["hand"]));
        Assert.Equal(0, Assert.IsType<int>(aliceZones["handHidden"]));
        Assert.Empty(StringList(bobZones["hand"]));
        Assert.Equal(1, Assert.IsType<int>(bobZones["handHidden"]));
        Assert.DoesNotContain("B-HAND-1", bobObjects.Keys);
        Assert.Contains("B-FIELD-1", bobObjects.Keys);
        Assert.Equal(1, Assert.IsType<int>(lanes["battlefieldCount"]));
    }

    [Fact]
    public void SnapshotsExposeBattlefieldControlOccupantsAndStandbyState()
    {
        var state = new MatchState(
            "battlefield-state-room",
            11,
            3,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "A-UNIT-1", "A-STANDBY-1"]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = ["B-UNIT-1"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("BF-1", cardNo: "OGN·275/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "alice", controllerId: "alice"),
                ["A-UNIT-1"] = new("A-UNIT-1", power: 2, tags: [CardObjectTags.UnitCard], ownerId: "alice", controllerId: "alice"),
                ["B-UNIT-1"] = new("B-UNIT-1", power: 3, tags: [CardObjectTags.UnitCard], ownerId: "bob", controllerId: "bob"),
                ["A-STANDBY-1"] = new("A-STANDBY-1", isFaceDown: true, tags: [CardObjectTags.UnitCard, CardObjectTags.Standby], ownerId: "alice", controllerId: "alice")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["A-UNIT-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["B-UNIT-1"] = new("bob", "BATTLEFIELD", "BF-1"),
                ["A-STANDBY-1"] = new("alice", "BATTLEFIELD", "BF-1")
            });

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var lanes = Assert.IsType<Dictionary<string, object?>>(snapshot.Lanes);
        var battlefields = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(lanes["battlefields"]);
        var battlefield = Assert.Single(battlefields);

        Assert.Equal("BF-1", Assert.IsType<string>(battlefield["battlefieldObjectId"]));
        Assert.Equal("alice", Assert.IsType<string>(battlefield["controllerId"]));
        Assert.Equal("CONTESTED", Assert.IsType<string>(battlefield["status"]));
        Assert.True(Assert.IsType<bool>(battlefield["contested"]));
        Assert.Equal(["A-STANDBY-1"], StringList(battlefield["standbyObjectIds"]));
        Assert.Equal(1, Assert.IsType<int>(battlefield["faceDownStandbyCount"]));
        Assert.Equal(["A-UNIT-1", "B-UNIT-1"], StringList(battlefield["occupantObjectIds"]));
    }

    [Fact]
    public void MatchStateBattlefieldControllerAndStandbyCleanupUseLegacyOwnershipFallback()
    {
        var state = new MatchState(
            "battlefield-legacy-controller-standby-room",
            12,
            3,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-LEGACY", "A-STANDBY-LEGACY"]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = ["B-STANDBY-LEGACY"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-LEGACY"] = new(
                    "BF-LEGACY",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "alice"),
                ["A-STANDBY-LEGACY"] = new(
                    "A-STANDBY-LEGACY",
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "alice"),
                ["B-STANDBY-LEGACY"] = new(
                    "B-STANDBY-LEGACY",
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "bob")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-LEGACY"] = new("alice", "BATTLEFIELD", "BF-LEGACY"),
                ["A-STANDBY-LEGACY"] = new("alice", "BATTLEFIELD", "BF-LEGACY"),
                ["B-STANDBY-LEGACY"] = new("bob", "BATTLEFIELD", "BF-LEGACY")
            });

        var battlefield = Assert.Single(state.BattlefieldStates.Values);
        Assert.Equal("alice", battlefield.ControllerId);
        Assert.Equal("CONTROLLED", battlefield.Status);
        Assert.False(battlefield.Contested);
        Assert.Equal(["A-STANDBY-LEGACY", "B-STANDBY-LEGACY"], battlefield.StandbyObjectIds);

        Assert.DoesNotContain(
            state.PendingCleanupTasks,
            task => string.Equals(task.Kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal)
                && string.Equals(task.ObjectId, "A-STANDBY-LEGACY", StringComparison.Ordinal));
        Assert.Contains(
            state.PendingCleanupTasks,
            task => string.Equals(task.Kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal)
                && string.Equals(task.ObjectId, "B-STANDBY-LEGACY", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-LEGACY", StringComparison.Ordinal));

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var lanes = Assert.IsType<Dictionary<string, object?>>(snapshot.Lanes);
        var battlefields = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(lanes["battlefields"]);
        var battlefieldView = Assert.Single(battlefields);
        Assert.Equal("alice", Assert.IsType<string>(battlefieldView["controllerId"]));
        Assert.Equal("CONTROLLED", Assert.IsType<string>(battlefieldView["status"]));
        Assert.Equal(["REMOVE_ILLEGAL_STANDBY"], StringList(battlefieldView["pendingTaskKinds"]));
    }

    [Fact]
    public async Task MatchStateExposesAuthoritativeBattlefieldAndCleanupTaskViews()
    {
        var state = new MatchState(
            "battlefield-authority-room",
            12,
            3,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "A-UNIT-1"]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = ["B-UNIT-1"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new(
                    "BF-1",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "alice",
                    controllerId: "alice"),
                ["A-UNIT-1"] = new(
                    "A-UNIT-1",
                    damage: 2,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "alice",
                    controllerId: "alice"),
                ["B-UNIT-1"] = new(
                    "B-UNIT-1",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "bob",
                    controllerId: "bob")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["A-UNIT-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["B-UNIT-1"] = new("bob", "BATTLEFIELD", "BF-1")
            });

        var battlefield = Assert.Single(state.BattlefieldStates.Values);
        Assert.Equal("BF-1", battlefield.BattlefieldObjectId);
        Assert.Equal("CONTESTED", battlefield.Status);
        Assert.Equal(["alice", "bob"], battlefield.OccupantControllerIds);
        Assert.Equal(["A-UNIT-1", "B-UNIT-1"], battlefield.OccupantObjectIds);

        Assert.Contains(
            state.PendingCleanupTasks,
            task => string.Equals(task.Kind, "DESTROY_LETHAL_UNIT", StringComparison.Ordinal)
                && string.Equals(task.ObjectId, "A-UNIT-1", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-1", StringComparison.Ordinal));
        Assert.Contains(
            state.PendingCleanupTasks,
            task => string.Equals(task.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-1", StringComparison.Ordinal));
        Assert.Contains(
            state.PendingCleanupTasks,
            task => string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
                && string.Equals(task.Reason, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-1", StringComparison.Ordinal));
        Assert.Contains(
            state.PendingCleanupTasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.Reason, "SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-1", StringComparison.Ordinal));

        Assert.Collection(
            state.BattlefieldTasks,
            task =>
            {
                Assert.Equal("START_SPELL_DUEL", task.Kind);
                Assert.Equal("PENDING", task.Status);
                Assert.Equal("BF-1", task.BattlefieldObjectId);
                Assert.Equal(["alice", "bob"], task.ParticipantControllerIds);
                Assert.Equal(["A-UNIT-1", "B-UNIT-1"], task.ParticipantObjectIds);
            },
            task =>
            {
                Assert.Equal("START_BATTLE", task.Kind);
                Assert.Equal("PENDING", task.Status);
                Assert.Equal("BF-1", task.BattlefieldObjectId);
                Assert.Equal(["alice", "bob"], task.ParticipantControllerIds);
                Assert.Equal(["A-UNIT-1", "B-UNIT-1"], task.ParticipantObjectIds);
            });
        Assert.True(state.PendingTaskQueue.HasTasks);
        Assert.True(state.PendingTaskQueue.IsBlocking);
        Assert.Equal("STATE_BASED_CLEANUP", state.PendingTaskQueue.Phase);
        Assert.Equal("cleanup:lethal:A-UNIT-1", state.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["DESTROY_LETHAL_UNIT", "BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            state.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Timing["battlefieldTasks"]);
        Assert.Collection(
            battlefieldTasks,
            task => Assert.Equal("START_SPELL_DUEL", Assert.IsType<string>(task["kind"])),
            task => Assert.Equal("START_BATTLE", Assert.IsType<string>(task["kind"])));
        var taskQueue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
        Assert.True(Assert.IsType<bool>(taskQueue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(taskQueue["isBlocking"]));
        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(taskQueue["phase"]));
        Assert.Equal("cleanup:lethal:A-UNIT-1", Assert.IsType<string>(taskQueue["activeTaskId"]));
        var taskQueueItems = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(taskQueue["tasks"]);
        Assert.Equal(4, taskQueueItems.Count);

        var prompts = ResolutionResult.BuildPrompts(state);
        Assert.False(prompts["alice"].Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], prompts["alice"].Actions);
        Assert.Contains("致命伤害清理", prompts["alice"].Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("DESTROY_LETHAL_UNIT", prompts["alice"].Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("PLAY_CARD", prompts["alice"].Actions);

        var blocked = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("blocked-end-turn", "alice", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);
        Assert.False(blocked.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, blocked.ErrorCode);
        Assert.Contains("致命伤害清理", blocked.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("DESTROY_LETHAL_UNIT", blocked.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MatchStateBattlefieldTasksUseLegacyOwnedOccupantControllers()
    {
        var state = new MatchState(
            "battlefield-legacy-controller-task-room",
            13,
            3,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "A-UNIT-LEGACY"]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = ["B-UNIT-LEGACY"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new(
                    "BF-1",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "alice",
                    controllerId: "alice"),
                ["A-UNIT-LEGACY"] = new(
                    "A-UNIT-LEGACY",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "alice"),
                ["B-UNIT-LEGACY"] = new(
                    "B-UNIT-LEGACY",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "bob")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["A-UNIT-LEGACY"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["B-UNIT-LEGACY"] = new("bob", "BATTLEFIELD", "BF-1")
            });

        var battlefield = Assert.Single(state.BattlefieldStates.Values);
        Assert.Equal("CONTESTED", battlefield.Status);
        Assert.True(battlefield.Contested);
        Assert.Equal(["alice", "bob"], battlefield.OccupantControllerIds);
        Assert.Equal(["A-UNIT-LEGACY", "B-UNIT-LEGACY"], battlefield.OccupantObjectIds);

        Assert.Collection(
            state.BattlefieldTasks,
            task =>
            {
                Assert.Equal("START_SPELL_DUEL", task.Kind);
                Assert.Equal(["alice", "bob"], task.ParticipantControllerIds);
                Assert.Equal(["A-UNIT-LEGACY", "B-UNIT-LEGACY"], task.ParticipantObjectIds);
            },
            task =>
            {
                Assert.Equal("START_BATTLE", task.Kind);
                Assert.Equal(["alice", "bob"], task.ParticipantControllerIds);
                Assert.Equal(["A-UNIT-LEGACY", "B-UNIT-LEGACY"], task.ParticipantObjectIds);
            });

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var lanes = Assert.IsType<Dictionary<string, object?>>(snapshot.Lanes);
        var battlefields = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(lanes["battlefields"]);
        var battlefieldView = Assert.Single(battlefields);
        Assert.Equal(["alice", "bob"], StringList(battlefieldView["occupantControllerIds"]));

        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Timing["battlefieldTasks"]);
        Assert.Collection(
            battlefieldTasks,
            task => Assert.Equal(["alice", "bob"], StringList(task["participantControllerIds"])),
            task => Assert.Equal(["alice", "bob"], StringList(task["participantControllerIds"])));
    }

    [Fact]
    public async Task PendingTaskQueueExposesZeroPowerFromPowerModifierAsStateBasedTask()
    {
        var state = new MatchState(
            "zero-power-modifier-task-room",
            14,
            4,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            turnPlayerId: "alice",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty,
                ["bob"] = PlayerZones.Empty with
                {
                    Base = ["B-ZERO-MODIFIER-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["B-ZERO-MODIFIER-UNIT"] = new(
                    "B-ZERO-MODIFIER-UNIT",
                    power: 0,
                    untilEndOfTurnPowerModifier: -4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "bob")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["B-ZERO-MODIFIER-UNIT"] = new("bob", "BASE")
            });

        var powerEffect = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.TargetObjectId, "B-ZERO-MODIFIER-UNIT", StringComparison.Ordinal));
        Assert.Equal(-4, powerEffect.PowerDelta);
        Assert.Equal(4, powerEffect.BasePower);
        Assert.Equal(0, powerEffect.EffectivePower);

        var cleanupTask = Assert.Single(state.PendingCleanupTasks);
        Assert.Equal("cleanup:zero-power:B-ZERO-MODIFIER-UNIT", cleanupTask.TaskId);
        Assert.Equal("DESTROY_ZERO_POWER_UNIT", cleanupTask.Kind);
        Assert.Equal("ZERO_POWER", cleanupTask.Reason);
        Assert.Equal("bob", cleanupTask.PlayerId);
        Assert.Equal("B-ZERO-MODIFIER-UNIT", cleanupTask.ObjectId);
        Assert.True(state.PendingTaskQueue.IsBlocking);
        Assert.Equal("STATE_BASED_CLEANUP", state.PendingTaskQueue.Phase);
        Assert.Equal(cleanupTask.TaskId, state.PendingTaskQueue.ActiveTaskId);

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var taskQueue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
        Assert.True(Assert.IsType<bool>(taskQueue["isBlocking"]));
        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(taskQueue["phase"]));
        var taskQueueItems = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(taskQueue["tasks"]);
        var taskView = Assert.Single(taskQueueItems);
        Assert.Equal("DESTROY_ZERO_POWER_UNIT", Assert.IsType<string>(taskView["kind"]));

        var prompt = ResolutionResult.BuildPrompts(state)["alice"];
        Assert.False(prompt.Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], prompt.Actions);
        Assert.Contains("0 战力清理", prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("DESTROY_ZERO_POWER_UNIT", prompt.Reason, StringComparison.Ordinal);

        var blocked = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("blocked-zero-power-end-turn", "alice", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);
        Assert.False(blocked.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, blocked.ErrorCode);
        Assert.Contains("0 战力清理", blocked.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("DESTROY_ZERO_POWER_UNIT", blocked.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PendingTaskQueueExposesIllegalStandbyCleanupAsStateBasedTask()
    {
        var state = new MatchState(
            "illegal-standby-task-room",
            12,
            3,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            turnPlayerId: "alice",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "A-STANDBY-1"]
                },
                ["bob"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new(
                    "BF-1",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "alice",
                    controllerId: "bob"),
                ["A-STANDBY-1"] = new(
                    "A-STANDBY-1",
                    cardNo: "OGN·121/298",
                    power: 2,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    ownerId: "alice",
                    controllerId: "alice")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["A-STANDBY-1"] = new("alice", "BATTLEFIELD", "BF-1")
            });

        var battlefield = Assert.Single(state.BattlefieldStates.Values);
        Assert.Equal("bob", battlefield.ControllerId);
        Assert.Empty(battlefield.OccupantObjectIds);
        Assert.Equal(["A-STANDBY-1"], battlefield.StandbyObjectIds);
        Assert.Equal(1, battlefield.FaceDownStandbyCount);

        var cleanupTask = Assert.Single(
            state.PendingCleanupTasks,
            task => string.Equals(task.Kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal));
        Assert.Equal("cleanup:illegal-standby:BF-1:A-STANDBY-1", cleanupTask.TaskId);
        Assert.Equal("BATTLEFIELD_CONTROL_CLEANUP", cleanupTask.Reason);
        Assert.Equal("alice", cleanupTask.PlayerId);
        Assert.Equal("A-STANDBY-1", cleanupTask.ObjectId);
        Assert.Equal("BF-1", cleanupTask.BattlefieldObjectId);

        Assert.True(state.PendingTaskQueue.HasTasks);
        Assert.True(state.PendingTaskQueue.IsBlocking);
        Assert.Equal("STATE_BASED_CLEANUP", state.PendingTaskQueue.Phase);
        Assert.Equal("cleanup:illegal-standby:BF-1:A-STANDBY-1", state.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["REMOVE_ILLEGAL_STANDBY"],
            state.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var taskQueue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(taskQueue["phase"]));
        Assert.Equal("cleanup:illegal-standby:BF-1:A-STANDBY-1", Assert.IsType<string>(taskQueue["activeTaskId"]));
        var taskQueueItems = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(taskQueue["tasks"]);
        var taskQueueItem = Assert.Single(taskQueueItems);
        Assert.Equal("REMOVE_ILLEGAL_STANDBY", Assert.IsType<string>(taskQueueItem["kind"]));
        Assert.Equal("BATTLEFIELD_CONTROL_CLEANUP", Assert.IsType<string>(taskQueueItem["reason"]));

        var prompts = ResolutionResult.BuildPrompts(state);
        Assert.False(prompts["alice"].Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], prompts["alice"].Actions);
        Assert.Contains("待命清理", prompts["alice"].Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("REMOVE_ILLEGAL_STANDBY", prompts["alice"].Reason, StringComparison.Ordinal);

        var blocked = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("blocked-illegal-standby-end-turn", "alice", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);
        Assert.False(blocked.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, blocked.ErrorCode);
        Assert.Contains("待命清理", blocked.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("REMOVE_ILLEGAL_STANDBY", blocked.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PendingTaskQueueExposesUnattachedBattlefieldEquipmentCleanupAsStateBasedTask()
    {
        var state = new MatchState(
            "unattached-equipment-task-room",
            12,
            3,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            turnPlayerId: "alice",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "A-UNATTACHED-EQUIPMENT"]
                },
                ["bob"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new(
                    "BF-1",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "alice",
                    controllerId: "alice"),
                ["A-UNATTACHED-EQUIPMENT"] = new(
                    "A-UNATTACHED-EQUIPMENT",
                    cardNo: "SFD·135/221",
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "alice",
                    controllerId: "bob")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["A-UNATTACHED-EQUIPMENT"] = new("alice", "BATTLEFIELD", "BF-1")
            });

        var cleanupTask = Assert.Single(
            state.PendingCleanupTasks,
            task => string.Equals(task.Kind, "RECALL_UNATTACHED_EQUIPMENT", StringComparison.Ordinal));
        Assert.Equal("cleanup:unattached-equipment:BF-1:A-UNATTACHED-EQUIPMENT", cleanupTask.TaskId);
        Assert.Equal("UNATTACHED_EQUIPMENT_CLEANUP", cleanupTask.Reason);
        Assert.Equal("bob", cleanupTask.PlayerId);
        Assert.Equal("A-UNATTACHED-EQUIPMENT", cleanupTask.ObjectId);
        Assert.Equal("BF-1", cleanupTask.BattlefieldObjectId);

        Assert.True(state.PendingTaskQueue.HasTasks);
        Assert.True(state.PendingTaskQueue.IsBlocking);
        Assert.Equal("STATE_BASED_CLEANUP", state.PendingTaskQueue.Phase);
        Assert.Equal(cleanupTask.TaskId, state.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["RECALL_UNATTACHED_EQUIPMENT"],
            state.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var taskQueue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(taskQueue["phase"]));
        var taskQueueItems = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(taskQueue["tasks"]);
        var taskQueueItem = Assert.Single(taskQueueItems);
        Assert.Equal("RECALL_UNATTACHED_EQUIPMENT", Assert.IsType<string>(taskQueueItem["kind"]));
        Assert.Equal("UNATTACHED_EQUIPMENT_CLEANUP", Assert.IsType<string>(taskQueueItem["reason"]));

        var prompts = ResolutionResult.BuildPrompts(state);
        Assert.False(prompts["alice"].Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], prompts["alice"].Actions);
        Assert.Contains("装备清理", prompts["alice"].Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("RECALL_UNATTACHED_EQUIPMENT", prompts["alice"].Reason, StringComparison.Ordinal);

        var blocked = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("blocked-unattached-equipment-end-turn", "alice", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);
        Assert.False(blocked.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, blocked.ErrorCode);
        Assert.Contains("装备清理", blocked.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("RECALL_UNATTACHED_EQUIPMENT", blocked.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingTaskQueueUsesSpellDuelTaskAsActiveWhileContestDuelIsOpen()
    {
        var state = new MatchState(
            "battlefield-spell-duel-task-room",
            12,
            3,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            turnPlayerId: "alice",
            phase: MatchPhases.Main,
            timingState: TimingStates.SpellDuelOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "A-UNIT-1"]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = ["B-UNIT-1"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new(
                    "BF-1",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "alice",
                    controllerId: "alice"),
                ["A-UNIT-1"] = new(
                    "A-UNIT-1",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "alice",
                    controllerId: "alice"),
                ["B-UNIT-1"] = new(
                    "B-UNIT-1",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "bob",
                    controllerId: "bob")
            },
            focusPlayerId: "alice",
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["A-UNIT-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["B-UNIT-1"] = new("bob", "BATTLEFIELD", "BF-1")
            });

        Assert.Collection(
            state.BattlefieldTasks,
            task =>
            {
                Assert.Equal("START_SPELL_DUEL", task.Kind);
                Assert.Equal("ACTIVE", task.Status);
                Assert.Equal("alice", task.ActingPlayerId);
            },
            task =>
            {
                Assert.Equal("START_BATTLE", task.Kind);
                Assert.Equal("WAITING_FOR_SPELL_DUEL", task.Status);
            });
        Assert.Equal("SPELL_DUEL_TASKS", state.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-1", state.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            state.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var taskQueue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("SPELL_DUEL_TASKS", Assert.IsType<string>(taskQueue["phase"]));
        Assert.Equal("task:start-spell-duel:BF-1", Assert.IsType<string>(taskQueue["activeTaskId"]));
    }

    [Fact]
    public void PendingTaskQueueUsesStartBattleTaskAfterContestSpellDuelCloses()
    {
        var state = new MatchState(
            "battlefield-start-battle-task-room",
            13,
            3,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            turnPlayerId: "alice",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "A-UNIT-1"]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = ["B-UNIT-1"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new(
                    "BF-1",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "alice",
                    controllerId: "alice"),
                ["A-UNIT-1"] = new(
                    "A-UNIT-1",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "alice",
                    controllerId: "alice"),
                ["B-UNIT-1"] = new(
                    "B-UNIT-1",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "bob",
                    controllerId: "bob")
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("BF-1")],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["A-UNIT-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["B-UNIT-1"] = new("bob", "BATTLEFIELD", "BF-1")
            });

        Assert.Collection(
            state.BattlefieldTasks,
            task =>
            {
                Assert.Equal("START_SPELL_DUEL", task.Kind);
                Assert.Equal("COMPLETED", task.Status);
            },
            task =>
            {
                Assert.Equal("START_BATTLE", task.Kind);
                Assert.Equal("PENDING", task.Status);
            });
        Assert.Equal("BATTLE_TASKS", state.PendingTaskQueue.Phase);
        Assert.Equal("task:start-battle:BF-1", state.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            state.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Timing["battlefieldTasks"]);
        Assert.Collection(
            battlefieldTasks,
            task =>
            {
                Assert.Equal("START_SPELL_DUEL", Assert.IsType<string>(task["kind"]));
                Assert.Equal("COMPLETED", Assert.IsType<string>(task["status"]));
            },
            task =>
            {
                Assert.Equal("START_BATTLE", Assert.IsType<string>(task["kind"]));
                Assert.Equal("PENDING", Assert.IsType<string>(task["status"]));
            });
        var taskQueue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("BATTLE_TASKS", Assert.IsType<string>(taskQueue["phase"]));
        Assert.Equal("task:start-battle:BF-1", Assert.IsType<string>(taskQueue["activeTaskId"]));

        var prompts = ResolutionResult.BuildPrompts(state);
        Assert.True(prompts["alice"].Actionable);
        Assert.Equal(["DECLARE_BATTLE", "SURRENDER"], prompts["alice"].Actions);
        Assert.Contains("争夺战场", prompts["alice"].Reason, StringComparison.Ordinal);
        var candidate = Assert.Single(
            prompts["alice"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, "DECLARE_BATTLE", StringComparison.Ordinal));
        Assert.Equal("DECLARE_BATTLE", candidate.Action);
        Assert.True(candidate.Enabled);
        Assert.Equal(["A-UNIT-1"], (candidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["B-UNIT-1"], (candidate.Targets ?? []).Select(target => target.Id).ToArray());
        Assert.Equal(["BF-1"], (candidate.Destinations ?? []).Select(destination => destination.Id).ToArray());
        Assert.Equal(["COMBAT_ASSIGNMENT"], (candidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
    }

    [Fact]
    public void ActionPromptFiltersPlayCardSourcesByImplementedTimingAndBaseCost()
    {
        var noManaState = new MatchState(
            "prompt-payable-source-room",
            14,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-MIGHTY-FAERIE", "P1-OPPONENT-CONTROLLED-PLAY-SOURCE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-MIGHTY-FAERIE"] = new(
                    "P1-MIGHTY-FAERIE",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-OPPONENT-CONTROLLED-PLAY-SOURCE"] = new(
                    "P1-OPPONENT-CONTROLLED-PLAY-SOURCE",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P2")
            });

        var noManaPrompt = ResolutionResult.BuildPrompts(noManaState)["P1"];
        var noManaPlayCandidate = Assert.Single(
            noManaPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.False(noManaPlayCandidate.Enabled);
        Assert.Empty(noManaPlayCandidate.Sources ?? []);

        var payableState = noManaState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(4, 0),
                ["P2"] = RunePool.Empty
            }
        };
        var payablePrompt = ResolutionResult.BuildPrompts(payableState)["P1"];
        var payablePlayCandidate = Assert.Single(
            payablePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.True(payablePlayCandidate.Enabled);
        Assert.Contains(
            payablePlayCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-MIGHTY-FAERIE", StringComparison.Ordinal));

        var metadata = Assert.IsType<Dictionary<string, object?>>(payablePlayCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var sourceRequirement = Assert.Single(sourceRequirements);
        Assert.Equal("P1-MIGHTY-FAERIE", Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal("SFD·125/221", Assert.IsType<string>(sourceRequirement["cardNo"]));
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["minTargetCount"]));
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["maxTargetCount"]));
        Assert.True(Assert.IsType<bool>(sourceRequirement["composable"]));
        var destinationChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["destinationChoices"]);
        Assert.Contains(destinationChoices, choice => string.Equals(choice.Id, "BASE", StringComparison.Ordinal));
        Assert.Contains(destinationChoices, choice => string.Equals(choice.Id, "BATTLEFIELD:P1-MAIN", StringComparison.Ordinal));
    }

    [Fact]
    public void ActionPromptExposesCrescentGuardReadyPaymentAfterSpell()
    {
        var paymentRuneObjectId = "P1-RUNE-PURPLE-CRESCENT-001";
        var paymentResourceAction = $"RECYCLE_RUNE:{paymentRuneObjectId}";
        var state = new MatchState(
            "prompt-crescent-guard-ready-room",
            15,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(4, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-CRESCENT-GUARD"],
                    Base = [paymentRuneObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-CRESCENT-GUARD"] = new(
                    "P1-UNIT-CRESCENT-GUARD",
                    cardNo: "UNL-122/219",
                    ownerId: "P1",
                    controllerId: "P1"),
                [paymentRuneObjectId] = new(
                    paymentRuneObjectId,
                    cardNo: "SFD-R06",
                    tags: [CardObjectTags.RuneCard, "COLOR:purple"],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            untilEndOfTurnEffects: ["PLAYED_SPELL_THIS_TURN:P1"]);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["paymentResourceChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]);

        Assert.Contains("SPEND_POWER:purple:1", optionalCostChoices);
        Assert.Contains(paymentResourceAction, paymentResourceChoices);
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["availablePower"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["availablePowerWithPaymentResources"]));
        Assert.Equal(RuneTrait.Purple, Assert.IsType<string>(paymentResourcePowerByChoice[paymentResourceAction]["trait"]));
    }

    [Fact]
    public void ActionPromptFiltersHideCardSourcesByPayableStandbyCosts()
    {
        var noManaState = new MatchState(
            "prompt-hide-card-source-room",
            15,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-STANDBY-TEEMO", "P1-STANDBY-UNKNOWN", "P1-STANDBY-OPPONENT-CONTROLLED"],
                    Battlefields = ["P1-BATTLEFIELD-BANDLE-TREE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-STANDBY-TEEMO"] = new(
                    "P1-STANDBY-TEEMO",
                    cardNo: "OGN·121/298",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-STANDBY-UNKNOWN"] = new(
                    "P1-STANDBY-UNKNOWN",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-STANDBY-OPPONENT-CONTROLLED"] = new(
                    "P1-STANDBY-OPPONENT-CONTROLLED",
                    cardNo: "OGN·121/298",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-BANDLE-TREE"] = new(
                    "P1-BATTLEFIELD-BANDLE-TREE",
                    cardNo: "OGN·278/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var noManaPrompt = ResolutionResult.BuildPrompts(noManaState)["P1"];
        var noManaHideCandidate = Assert.Single(
            noManaPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "HIDE_CARD", StringComparison.Ordinal));
        Assert.False(noManaHideCandidate.Enabled);
        Assert.Empty(noManaHideCandidate.Sources ?? []);
        Assert.Empty(noManaHideCandidate.OptionalCosts ?? []);
        var noManaMetadata = Assert.IsType<Dictionary<string, object?>>(noManaHideCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            noManaMetadata["sourceRequirements"]));

        var payableState = noManaState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(noManaState.PlayerZones, StringComparer.Ordinal)
            {
                ["P1"] = noManaState.PlayerZones["P1"] with
                {
                    LegendZone = ["P1-LEGEND-TEEMO"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(noManaState.CardObjects, StringComparer.Ordinal)
            {
                ["P1-LEGEND-TEEMO"] = new(
                    "P1-LEGEND-TEEMO",
                    cardNo: "OGN·263/298",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var payablePrompt = ResolutionResult.BuildPrompts(payableState)["P1"];
        var payableHideCandidate = Assert.Single(
            payablePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "HIDE_CARD", StringComparison.Ordinal));
        Assert.True(payableHideCandidate.Enabled);
        Assert.Equal("布置待命", payableHideCandidate.Label);
        Assert.Equal(["P1-STANDBY-TEEMO"], (payableHideCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(
            ["STANDBY_A", "STANDBY_TEEMO_MANA"],
            (payableHideCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        Assert.Equal(
            ["STANDBY", "BATTLEFIELD:P1-BATTLEFIELD-BANDLE-TREE"],
            (payableHideCandidate.Destinations ?? []).Select(destination => destination.Id).ToArray());

        var payableMetadata = Assert.IsType<Dictionary<string, object?>>(payableHideCandidate.Metadata);
        var sourceRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            payableMetadata["sourceRequirements"]));
        Assert.Equal("P1-STANDBY-TEEMO", Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal("OGN·121/298", Assert.IsType<string>(sourceRequirement["cardNo"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["manaCost"]));
        Assert.True(Assert.IsType<bool>(sourceRequirement["composable"]));
        Assert.Equal(
            ["STANDBY_A", "STANDBY_TEEMO_MANA"],
            Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(sourceRequirement["optionalCostChoices"])
                .Select(cost => cost.Id)
                .ToArray());
        Assert.Equal(
            ["STANDBY", "BATTLEFIELD:P1-BATTLEFIELD-BANDLE-TREE"],
            Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(sourceRequirement["destinationChoices"])
                .Select(destination => destination.Id)
                .ToArray());

        var dirtyDestinationState = payableState with
        {
            CardObjects = new Dictionary<string, CardObjectState>(payableState.CardObjects, StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-BANDLE-TREE"] = payableState.CardObjects["P1-BATTLEFIELD-BANDLE-TREE"] with
                {
                    ControllerId = "P2"
                }
            }
        };
        var dirtyPrompt = ResolutionResult.BuildPrompts(dirtyDestinationState)["P1"];
        var dirtyHideCandidate = Assert.Single(
            dirtyPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "HIDE_CARD", StringComparison.Ordinal));
        Assert.True(dirtyHideCandidate.Enabled);
        Assert.Equal(["STANDBY"], (dirtyHideCandidate.Destinations ?? []).Select(destination => destination.Id).ToArray());
        var dirtyMetadata = Assert.IsType<Dictionary<string, object?>>(dirtyHideCandidate.Metadata);
        var dirtyRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            dirtyMetadata["sourceRequirements"]));
        Assert.Equal(
            ["STANDBY"],
            Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(dirtyRequirement["destinationChoices"])
                .Select(destination => destination.Id)
                .ToArray());

        var freeState = noManaState with
        {
            UntilEndOfTurnEffects = ["FREE_STANDBY_HIDE:P1"]
        };
        var freePrompt = ResolutionResult.BuildPrompts(freeState)["P1"];
        var freeHideCandidate = Assert.Single(
            freePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "HIDE_CARD", StringComparison.Ordinal));
        Assert.True(freeHideCandidate.Enabled);
        Assert.Equal(["STANDBY_FREE"], (freeHideCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
    }

    [Fact]
    public void ActionPromptFiltersRevealCardSourcesByWindowAndFaceDownStandby()
    {
        var openState = new MatchState(
            "prompt-reveal-card-source-room",
            18,
            4,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [
                        "P1-FACE-DOWN-STANDBY",
                        "P1-FACE-DOWN-UNKNOWN-STANDBY",
                        "P1-FACE-DOWN-OPPONENT-CONTROLLED",
                        "P1-FACE-UP-STANDBY",
                        "P1-FACE-DOWN-NON-STANDBY"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FACE-DOWN-STANDBY"] = new(
                    "P1-FACE-DOWN-STANDBY",
                    isFaceDown: true,
                    power: 1,
                    cardNo: "OGN·197/298",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FACE-DOWN-UNKNOWN-STANDBY"] = new(
                    "P1-FACE-DOWN-UNKNOWN-STANDBY",
                    isFaceDown: true,
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FACE-DOWN-OPPONENT-CONTROLLED"] = new(
                    "P1-FACE-DOWN-OPPONENT-CONTROLLED",
                    isFaceDown: true,
                    power: 1,
                    cardNo: "OGN·197/298",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-FACE-UP-STANDBY"] = new(
                    "P1-FACE-UP-STANDBY",
                    isFaceDown: false,
                    power: 1,
                    cardNo: "OGN·197/298",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FACE-DOWN-NON-STANDBY"] = new(
                    "P1-FACE-DOWN-NON-STANDBY",
                    isFaceDown: true,
                    power: 1,
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var openPrompt = ResolutionResult.BuildPrompts(openState)["P1"];
        var openRevealCandidate = Assert.Single(
            openPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "REVEAL_CARD", StringComparison.Ordinal));
        Assert.True(openRevealCandidate.Enabled);
        Assert.Equal("翻开待命", openRevealCandidate.Label);
        Assert.Equal(["P1-FACE-DOWN-STANDBY"], (openRevealCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["STANDBY_REVEAL"], (openRevealCandidate.Modes ?? []).Select(mode => mode.Id).ToArray());
        Assert.Equal(["BASE"], (openRevealCandidate.Destinations ?? []).Select(destination => destination.Id).ToArray());
        Assert.Equal(["STANDBY_REVEAL_0"], (openRevealCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());

        var openMetadata = Assert.IsType<Dictionary<string, object?>>(openRevealCandidate.Metadata);
        var openRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            openMetadata["sourceRequirements"]));
        Assert.Equal("P1-FACE-DOWN-STANDBY", Assert.IsType<string>(openRequirement["sourceObjectId"]));
        Assert.Equal("OGN·197/298", Assert.IsType<string>(openRequirement["cardNo"]));
        Assert.Equal("STANDBY_REVEAL", Assert.IsType<string>(openRequirement["mode"]));
        Assert.Equal("翻开待命", Assert.IsType<string>(openRequirement["modeLabel"]));
        Assert.True(Assert.IsType<bool>(openRequirement["composable"]));
        Assert.Equal(
            ["BASE"],
            Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(openRequirement["destinationChoices"])
                .Select(destination => destination.Id)
                .ToArray());
        Assert.Equal(
            ["STANDBY_REVEAL_0"],
            Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(openRequirement["optionalCostChoices"])
                .Select(cost => cost.Id)
                .ToArray());
        Assert.Equal(
            ["STANDBY_REVEAL_0"],
            Assert.IsAssignableFrom<IEnumerable<string>>(openRequirement["requiredOptionalCosts"]).ToArray());

        var closedWithoutStackState = openState with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1"
        };
        var closedWithoutStackPrompt = ResolutionResult.BuildPrompts(closedWithoutStackState)["P1"];
        var closedWithoutStackRevealCandidate = Assert.Single(
            closedWithoutStackPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "REVEAL_CARD", StringComparison.Ordinal));
        Assert.False(closedWithoutStackRevealCandidate.Enabled);
        Assert.Empty(closedWithoutStackRevealCandidate.Sources ?? []);

        var reactionState = openState with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            StackItems = [
                new StackItemState(
                    "STACK-PENDING-001",
                    "P2",
                    "P2-SPELL-PENDING-001",
                    "DRAW_1",
                    "OGN·007/298")
            ]
        };
        var reactionPrompt = ResolutionResult.BuildPrompts(reactionState)["P1"];
        Assert.Contains("REVEAL_CARD", reactionPrompt.Actions);
        var reactionRevealCandidate = Assert.Single(
            reactionPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "REVEAL_CARD", StringComparison.Ordinal));
        Assert.True(reactionRevealCandidate.Enabled);
        Assert.Equal(["P1-FACE-DOWN-STANDBY"], (reactionRevealCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["STANDBY_REACTION"], (reactionRevealCandidate.Modes ?? []).Select(mode => mode.Id).ToArray());
        Assert.Equal(["STACK"], (reactionRevealCandidate.Destinations ?? []).Select(destination => destination.Id).ToArray());

        var opponentPrompt = ResolutionResult.BuildPrompts(reactionState)["P2"];
        Assert.Equal(["WAIT", "SURRENDER"], opponentPrompt.Actions);
    }

    [Fact]
    public void ActionPromptExposesPlayableReactionCardsDuringStackPriority()
    {
        var state = new MatchState(
            "prompt-priority-reaction-room",
            19,
            5,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralClosed,
            priorityPlayerId: "P2",
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 0)
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-HARD-BARGAIN", "P2-HAND-ORDINARY-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-SPELL-HARD-BARGAIN"] = new(
                    "P2-SPELL-HARD-BARGAIN",
                    cardNo: "SFD·136/221",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 2,
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-HAND-ORDINARY-UNIT"] = new(
                    "P2-HAND-ORDINARY-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            stackItems:
            [
                new StackItemState(
                    "STACK-1-P1-SPELL-INCINERATE",
                    "P1",
                    "P1-SPELL-INCINERATE",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024")
            ]);

        var prompt = ResolutionResult.BuildPrompts(state)["P2"];

        Assert.Equal(["PLAY_CARD", "PASS_PRIORITY", "SURRENDER"], prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Equal(["P2-SPELL-HARD-BARGAIN"], (playCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["STACK-1-P1-SPELL-INCINERATE"], (playCandidate.Targets ?? []).Select(target => target.Id).ToArray());
        Assert.Equal(["TARGET_DECLINES_PAY_2_NO_ECHO"], (playCandidate.Modes ?? []).Select(mode => mode.Id).ToArray());

        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
        Assert.Equal("P2-SPELL-HARD-BARGAIN", Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal("SFD·136/221", Assert.IsType<string>(sourceRequirement["cardNo"]));
        Assert.Equal("TARGET_DECLINES_PAY_2_NO_ECHO", Assert.IsType<string>(sourceRequirement["mode"]));
        Assert.True(Assert.IsType<bool>(sourceRequirement["composable"]));

        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var firstTargetChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            targetChoicesByIndex["0"]);
        Assert.Equal(["STACK-1-P1-SPELL-INCINERATE"], firstTargetChoices.Select(choice => choice.Id).ToArray());

        var opponentPrompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.Equal(["WAIT", "SURRENDER"], opponentPrompt.Actions);
    }

    [Fact]
    public void ActionPromptPlayCardMetadataFiltersTargetsBySourceRequirement()
    {
        var state = new MatchState(
            "prompt-play-target-room",
            15,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HEXTECH-RAY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HEXTECH-RAY"] = new(
                    "P1-HEXTECH-RAY",
                    cardNo: "OGN·009/298",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-UNIT"] = new(
                    "P2-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));

        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Targets ?? [],
            target => string.Equals(target.Id, "P2-BATTLEFIELD-UNIT", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var sourceRequirement = Assert.Single(sourceRequirements);
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["minTargetCount"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["maxTargetCount"]));
        Assert.Equal("BATTLEFIELD_UNIT", Assert.IsType<string>(sourceRequirement["targetScope"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var firstTargetChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            targetChoicesByIndex["0"]);
        Assert.Contains(firstTargetChoices, choice => string.Equals(choice.Id, "P2-BATTLEFIELD-UNIT", StringComparison.Ordinal));
    }

    [Fact]
    public void ActionPromptPlayCardMetadataFiltersEnemyTargetsByZoneController()
    {
        var state = new MatchState(
            "prompt-play-enemy-target-control-room",
            18,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
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
                    Hand = ["P1-SPELL-HIGHWAY-ROBBERY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base =
                    [
                        "P2-ENEMY-UNIT",
                        "P2-DIRTY-P1-OWNED-UNIT"
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HIGHWAY-ROBBERY"] = new(
                    "P1-SPELL-HIGHWAY-ROBBERY",
                    cardNo: "OGN·033/298",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-ENEMY-UNIT"] = new(
                    "P2-ENEMY-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DIRTY-P1-OWNED-UNIT"] = new(
                    "P2-DIRTY-P1-OWNED-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));

        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Targets ?? [],
            target => string.Equals(target.Id, "P2-ENEMY-UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(
            playCandidate.Targets ?? [],
            target => string.Equals(target.Id, "P2-DIRTY-P1-OWNED-UNIT", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();
        Assert.All(sourceRequirements, requirement =>
        {
            var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
                requirement["targetChoicesByIndex"]);
            var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                choicesByIndex["0"]);
            Assert.DoesNotContain(
                choices,
                choice => string.Equals(choice.Id, "P2-DIRTY-P1-OWNED-UNIT", StringComparison.Ordinal));
        });
        var sourceRequirement = Assert.Single(sourceRequirements, requirement => requirement["mode"] is null);
        Assert.Equal("ENEMY_UNIT", Assert.IsType<string>(sourceRequirement["targetScope"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var firstTargetChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            targetChoicesByIndex["0"]).ToArray();
        Assert.Contains(firstTargetChoices, choice => string.Equals(choice.Id, "P2-ENEMY-UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(
            firstTargetChoices,
            choice => string.Equals(choice.Id, "P2-DIRTY-P1-OWNED-UNIT", StringComparison.Ordinal));
    }

    [Fact]
    public void ActionPromptPlayCardMetadataFiltersFriendlyTargetsByController()
    {
        var state = new MatchState(
            "prompt-play-friendly-target-control-room",
            16,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
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
                    Hand = ["P1-UNIT-ARENA-ROOKIE"],
                    Base =
                    [
                        "P1-FRIENDLY-TARGET",
                        "P1-DIRTY-OPPONENT-CONTROLLED-TARGET"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-ARENA-ROOKIE"] = new(
                    "P1-UNIT-ARENA-ROOKIE",
                    cardNo: "OGN·136/298",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FRIENDLY-TARGET"] = new(
                    "P1-FRIENDLY-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-DIRTY-OPPONENT-CONTROLLED-TARGET"] = new(
                    "P1-DIRTY-OPPONENT-CONTROLLED-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));

        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Targets ?? [],
            target => string.Equals(target.Id, "P1-FRIENDLY-TARGET", StringComparison.Ordinal));
        Assert.DoesNotContain(
            playCandidate.Targets ?? [],
            target => string.Equals(target.Id, "P1-DIRTY-OPPONENT-CONTROLLED-TARGET", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var sourceRequirement = Assert.Single(sourceRequirements);
        Assert.Equal("FRIENDLY_UNIT", Assert.IsType<string>(sourceRequirement["targetScope"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var firstTargetChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            targetChoicesByIndex["0"]);
        Assert.Equal(["P1-FRIENDLY-TARGET"], firstTargetChoices.Select(choice => choice.Id).ToArray());
    }

    [Fact]
    public void ActionPromptPlayCardMetadataFiltersFriendlyHandTargetsByController()
    {
        var state = new MatchState(
            "prompt-play-friendly-hand-target-control-room",
            17,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
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
                    Hand =
                    [
                        "P1-SPELL-HELP-ARRIVES",
                        "P1-FRIENDLY-HAND-UNIT",
                        "P1-DIRTY-OPPONENT-CONTROLLED-HAND-UNIT"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HELP-ARRIVES"] = new(
                    "P1-SPELL-HELP-ARRIVES",
                    cardNo: "SFD·111/221",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FRIENDLY-HAND-UNIT"] = new(
                    "P1-FRIENDLY-HAND-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-DIRTY-OPPONENT-CONTROLLED-HAND-UNIT"] = new(
                    "P1-DIRTY-OPPONENT-CONTROLLED-HAND-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));

        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Targets ?? [],
            target => string.Equals(target.Id, "P1-FRIENDLY-HAND-UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(
            playCandidate.Targets ?? [],
            target => string.Equals(target.Id, "P1-DIRTY-OPPONENT-CONTROLLED-HAND-UNIT", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var sourceRequirement = Assert.Single(sourceRequirements);
        Assert.Equal("FRIENDLY_HAND_CARD", Assert.IsType<string>(sourceRequirement["targetScope"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var firstTargetChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            targetChoicesByIndex["0"]).ToArray();
        Assert.Contains(firstTargetChoices, choice => string.Equals(choice.Id, "P1-FRIENDLY-HAND-UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(
            firstTargetChoices,
            choice => string.Equals(choice.Id, "P1-DIRTY-OPPONENT-CONTROLLED-HAND-UNIT", StringComparison.Ordinal));
    }

    [Fact]
    public void ActionPromptHidesPlayCardSourceWhenSpellshieldTaxLeavesNoLegalTargetSelection()
    {
        var insufficientState = new MatchState(
            "prompt-play-spellshield-tax-room",
            20,
            6,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
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
                    Hand = ["P1-SPELL-INCINERATE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SPELLSHIELD-TAX-TARGET-001"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-INCINERATE"] = new(
                    "P1-SPELL-INCINERATE",
                    cardNo: "OGS·003/024",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 2,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-SPELLSHIELD-TAX-TARGET-001"] = new(
                    "P2-SPELLSHIELD-TAX-TARGET-001",
                    cardNo: "OGN·013/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    ownerId: "P2",
                    controllerId: "P2")
            });

        var insufficientPrompt = ResolutionResult.BuildPrompts(insufficientState)["P1"];
        var insufficientPlayCandidate = Assert.Single(
            insufficientPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.False(insufficientPlayCandidate.Enabled);
        Assert.Empty(insufficientPlayCandidate.Sources ?? []);
        var insufficientMetadata = Assert.IsType<Dictionary<string, object?>>(insufficientPlayCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            insufficientMetadata["sourceRequirements"]));

        var payableState = insufficientState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(3, 0),
                ["P2"] = RunePool.Empty
            }
        };
        var payablePrompt = ResolutionResult.BuildPrompts(payableState)["P1"];
        var payablePlayCandidate = Assert.Single(
            payablePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.True(payablePlayCandidate.Enabled);
        Assert.Equal(["P1-SPELL-INCINERATE"], (payablePlayCandidate.Sources ?? []).Select(source => source.Id).ToArray());

        var payableMetadata = Assert.IsType<Dictionary<string, object?>>(payablePlayCandidate.Metadata);
        var sourceRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            payableMetadata["sourceRequirements"]));
        Assert.Equal("P1-SPELL-INCINERATE", Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal("OGS·003/024", Assert.IsType<string>(sourceRequirement["cardNo"]));
        var legalTargetSelections = Assert.IsAssignableFrom<IEnumerable<IReadOnlyList<string>>>(
                sourceRequirement["legalTargetSelections"])
            .Select(selection => selection.ToArray())
            .ToArray();
        Assert.Contains(legalTargetSelections, selection => selection.SequenceEqual(["P2-SPELLSHIELD-TAX-TARGET-001"]));
    }

    [Fact]
    public void ActionPromptHidesPlayCardSourceWhenHandObjectHasNoCardNo()
    {
        var state = new MatchState(
            "prompt-play-unknown-source-room",
            21,
            6,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
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
                    Hand = ["P1-HAND-UNKNOWN-PLAY-SOURCE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNKNOWN-PLAY-SOURCE"] = new(
                    "P1-HAND-UNKNOWN-PLAY-SOURCE",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));

        Assert.False(playCandidate.Enabled);
        Assert.Empty(playCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
    }

    [Fact]
    public void ActionPromptHidesPlayCardTargetWhenObjectHasNoCardNo()
    {
        var state = new MatchState(
            "prompt-play-unknown-target-room",
            22,
            6,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HEXTECH-RAY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-UNKNOWN-PLAY-TARGET"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HEXTECH-RAY"] = new(
                    "P1-SPELL-HEXTECH-RAY",
                    cardNo: "OGN·009/298",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-UNIT-UNKNOWN-PLAY-TARGET"] = new(
                    "P2-UNIT-UNKNOWN-PLAY-TARGET",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));

        Assert.False(playCandidate.Enabled);
        Assert.Empty(playCandidate.Sources ?? []);
        Assert.Empty(playCandidate.Targets ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
    }

    [Fact]
    public void ActionPromptSpellDuelFocusOnlyExposesPlayCardWhenSourceIsComposable()
    {
        var emptyFocusState = new MatchState(
            "prompt-spell-duel-focus-room",
            16,
            4,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.SpellDuelOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            focusPlayerId: "P1");

        var emptyPrompt = ResolutionResult.BuildPrompts(emptyFocusState)["P1"];
        Assert.Equal(["PASS_FOCUS", "SURRENDER"], emptyPrompt.Actions);
        Assert.DoesNotContain(
            emptyPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));

        var playableFocusState = emptyFocusState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HEXTECH-RAY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HEXTECH-RAY"] = new(
                    "P1-HEXTECH-RAY",
                    cardNo: "OGN·009/298",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-UNIT"] = new(
                    "P2-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var playablePrompt = ResolutionResult.BuildPrompts(playableFocusState)["P1"];
        Assert.Equal(["PLAY_CARD", "PASS_FOCUS", "SURRENDER"], playablePrompt.Actions);
        var playCandidate = Assert.Single(
            playablePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-HEXTECH-RAY", StringComparison.Ordinal));
        Assert.Contains(
            playCandidate.Targets ?? [],
            target => string.Equals(target.Id, "P2-BATTLEFIELD-UNIT", StringComparison.Ordinal));
    }

    [Fact]
    public void ActionPromptFiltersMoveUnitSourcesToFaceUpNonCombatUnits()
    {
        var state = new MatchState(
            "prompt-move-source-room",
            15,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base =
                    [
                        "P1-READY-UNIT",
                        "P1-LEGACY-OWNED-UNIT",
                        "P1-FACEDOWN-UNIT",
                        "P1-ATTACKING-UNIT",
                        "P1-OPPONENT-CONTROLLED-UNIT",
                        "P1-DIRTY-P2-OWNED-UNIT"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-READY-UNIT"] = new(
                    "P1-READY-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGACY-OWNED-UNIT"] = new(
                    "P1-LEGACY-OWNED-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: ""),
                ["P1-FACEDOWN-UNIT"] = new(
                    "P1-FACEDOWN-UNIT",
                    isFaceDown: true,
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-ATTACKING-UNIT"] = new(
                    "P1-ATTACKING-UNIT",
                    isAttacking: true,
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-OPPONENT-CONTROLLED-UNIT"] = new(
                    "P1-OPPONENT-CONTROLLED-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-DIRTY-P2-OWNED-UNIT"] = new(
                    "P1-DIRTY-P2-OWNED-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var moveCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "MOVE_UNIT", StringComparison.Ordinal));
        Assert.True(moveCandidate.Enabled);
        Assert.Equal(["P1-READY-UNIT", "P1-LEGACY-OWNED-UNIT"], (moveCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["BATTLEFIELD"], (moveCandidate.Destinations ?? []).Select(destination => destination.Id).ToArray());
        Assert.Null(moveCandidate.OptionalCosts);
        var metadata = Assert.IsType<Dictionary<string, object?>>(moveCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();
        Assert.Equal(["P1-READY-UNIT", "P1-LEGACY-OWNED-UNIT"], sourceRequirements.Select(requirement => Assert.IsType<string>(requirement["sourceObjectId"])).ToArray());
        var sourceRequirement = Assert.Single(sourceRequirements, requirement =>
            string.Equals(Assert.IsType<string>(requirement["sourceObjectId"]), "P1-READY-UNIT", StringComparison.Ordinal));
        Assert.Equal("P1-READY-UNIT", Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal("BASE", Assert.IsType<string>(sourceRequirement["origin"]));
        Assert.Equal("BASE_TO_BATTLEFIELD", Assert.IsType<string>(sourceRequirement["mode"]));
        var destinationChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["destinationChoices"]);
        Assert.Equal(["BATTLEFIELD"], destinationChoices.Select(destination => destination.Id).ToArray());
    }

    [Fact]
    public void ActionPromptHidesMoveUnitSourceWhenUnitHasNoCardNo()
    {
        var state = new MatchState(
            "prompt-move-unknown-source-room",
            15,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNKNOWN-MOVE-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNKNOWN-MOVE-UNIT"] = new(
                    "P1-UNKNOWN-MOVE-UNIT",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var moveCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "MOVE_UNIT", StringComparison.Ordinal));
        Assert.False(moveCandidate.Enabled);
        Assert.Empty(moveCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(moveCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
    }

    [Fact]
    public void ActionPromptHidesMoveUnitBattlefieldDestinationWhenCardHasNoCardNo()
    {
        var state = new MatchState(
            "prompt-move-unknown-battlefield-room",
            15,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN",
                        "P1-UNIT-ROAM-MOVE-DESTINATION-FILTER",
                        "P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"] = new(
                    "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-ROAM-MOVE-DESTINATION-FILTER"] = new(
                    "P1-UNIT-ROAM-MOVE-DESTINATION-FILTER",
                    cardNo: "SFD·096/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "游走"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION"] = new(
                    "P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"),
                ["P1-UNIT-ROAM-MOVE-DESTINATION-FILTER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"),
                ["P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var moveCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "MOVE_UNIT", StringComparison.Ordinal));

        Assert.True(moveCandidate.Enabled);
        Assert.Contains(moveCandidate.Sources ?? [], source => string.Equals(source.Id, "P1-UNIT-ROAM-MOVE-DESTINATION-FILTER", StringComparison.Ordinal));
        Assert.DoesNotContain(
            moveCandidate.Destinations ?? [],
            choice => string.Equals(choice.Id, "BATTLEFIELD:P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(moveCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var roamRequirement = Assert.Single(sourceRequirements, requirement =>
            string.Equals(requirement["mode"] as string, "ROAM", StringComparison.Ordinal));
        var destinationChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            roamRequirement["destinationChoices"]);
        Assert.Equal(["BATTLEFIELD:P1-MAIN"], destinationChoices.Select(choice => choice.Id).ToArray());
    }

    [Fact]
    public void ActionPromptHidesRuneSourcesWhenRuneHasNoCardNo()
    {
        var state = new MatchState(
            "prompt-rune-unknown-source-room",
            15,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNKNOWN-RUNE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNKNOWN-RUNE"] = new(
                    "P1-UNKNOWN-RUNE",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var tapCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "TAP_RUNE", StringComparison.Ordinal));
        Assert.False(tapCandidate.Enabled);
        Assert.Empty(tapCandidate.Sources ?? []);
        Assert.Equal("横置符文 当前没有服务端可执行候选", tapCandidate.Reason);
        Assert.DoesNotContain("TAP_RUNE", tapCandidate.Reason);

        var recycleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "RECYCLE_RUNE", StringComparison.Ordinal));
        Assert.False(recycleCandidate.Enabled);
        Assert.Empty(recycleCandidate.Sources ?? []);
        Assert.Equal("回收符文 当前没有服务端可执行候选", recycleCandidate.Reason);
        Assert.DoesNotContain("RECYCLE_RUNE", recycleCandidate.Reason);
    }

    [Fact]
    public void ActionPromptFiltersAssembleEquipmentSourcesBySupportedAttachmentAndPower()
    {
        var noPowerState = new MatchState(
            "prompt-assemble-source-room",
            16,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-LONG-SWORD", "P1-LONG-SWORD-OPPONENT-CONTROLLED", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LONG-SWORD"] = new(
                    "P1-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LONG-SWORD-OPPONENT-CONTROLLED"] = new(
                    "P1-LONG-SWORD-OPPONENT-CONTROLLED",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var noPowerPrompt = ResolutionResult.BuildPrompts(noPowerState)["P1"];
        var noPowerCandidate = Assert.Single(
            noPowerPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.False(noPowerCandidate.Enabled);
        Assert.Empty(noPowerCandidate.Sources ?? []);

        var genericPowerState = noPowerState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(0, 1),
                ["P2"] = RunePool.Empty
            }
        };
        var genericPowerPrompt = ResolutionResult.BuildPrompts(genericPowerState)["P1"];
        var genericPowerCandidate = Assert.Single(
            genericPowerPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.False(genericPowerCandidate.Enabled);
        Assert.Empty(genericPowerCandidate.Sources ?? []);

        var recyclePaymentState = noPowerState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-LONG-SWORD", "P1-UNIT", "P1-RUNE-RED-ASSEMBLE-PAYMENT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(noPowerState.CardObjects, StringComparer.Ordinal)
            {
                ["P1-RUNE-RED-ASSEMBLE-PAYMENT"] = new(
                    "P1-RUNE-RED-ASSEMBLE-PAYMENT",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var recyclePaymentPrompt = ResolutionResult.BuildPrompts(recyclePaymentState)["P1"];
        var recyclePaymentCandidate = Assert.Single(
            recyclePaymentPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(recyclePaymentCandidate.Enabled);
        Assert.Equal(["P1-LONG-SWORD"], (recyclePaymentCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        var recyclePaymentMetadata = Assert.IsType<Dictionary<string, object?>>(recyclePaymentCandidate.Metadata);
        var recyclePaymentRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            recyclePaymentMetadata["sourceRequirements"]);
        var recyclePaymentRequirement = Assert.Single(recyclePaymentRequirements);
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            recyclePaymentRequirement["paymentResourceChoices"]);
        Assert.Equal(["RECYCLE_RUNE:P1-RUNE-RED-ASSEMBLE-PAYMENT"], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            recyclePaymentRequirement["paymentResourcePowerByChoice"]);
        var paymentResourcePower = paymentResourcePowerByChoice["RECYCLE_RUNE:P1-RUNE-RED-ASSEMBLE-PAYMENT"];
        Assert.Equal(RuneTrait.Red, Assert.IsType<string>(paymentResourcePower["trait"]));
        Assert.Equal(1, Assert.IsType<int>(paymentResourcePower["power"]));

        var payableState = noPowerState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            }
        };
        var payablePrompt = ResolutionResult.BuildPrompts(payableState)["P1"];
        var payableCandidate = Assert.Single(
            payablePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(payableCandidate.Enabled);
        Assert.Equal(["P1-LONG-SWORD"], (payableCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["P1-UNIT"], (payableCandidate.Targets ?? []).Select(target => target.Id).ToArray());
        Assert.Equal(["ASSEMBLE_RED"], (payableCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());

        var metadata = Assert.IsType<Dictionary<string, object?>>(payableCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var sourceRequirement = Assert.Single(sourceRequirements);
        Assert.Equal("P1-LONG-SWORD", Assert.IsType<string>(sourceRequirement["sourceObjectId"]));
        Assert.Equal("SFD·022/221", Assert.IsType<string>(sourceRequirement["equipmentCardNo"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["powerCost"]));
        Assert.True(Assert.IsType<bool>(sourceRequirement["composable"]));
        var targetChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["targetChoices"]);
        Assert.Equal(["P1-UNIT"], targetChoices.Select(target => target.Id).ToArray());
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_RED"], optionalCostChoices.Select(cost => cost.Id).ToArray());
        var requiredOptionalCosts = Assert.IsAssignableFrom<IEnumerable<string>>(
            sourceRequirement["requiredOptionalCosts"]);
        Assert.Equal(["ASSEMBLE_RED"], requiredOptionalCosts.ToArray());

        var jaggedDirkState = payableState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-JAGGED-DIRK", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-JAGGED-DIRK"] = new(
                    "P1-JAGGED-DIRK",
                    cardNo: "SFD·009/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var jaggedDirkPrompt = ResolutionResult.BuildPrompts(jaggedDirkState)["P1"];
        var jaggedDirkCandidate = Assert.Single(
            jaggedDirkPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(jaggedDirkCandidate.Enabled);
        Assert.Equal(["P1-JAGGED-DIRK"], (jaggedDirkCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_RED"], (jaggedDirkCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var jaggedDirkMetadata = Assert.IsType<Dictionary<string, object?>>(jaggedDirkCandidate.Metadata);
        var jaggedDirkRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(jaggedDirkMetadata["sourceRequirements"]));
        Assert.Equal("SFD·009/221", Assert.IsType<string>(jaggedDirkRequirement["equipmentCardNo"]));
        var jaggedDirkOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            jaggedDirkRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_RED"], jaggedDirkOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var recurveBowState = payableState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-RECURVE-BOW", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-RECURVE-BOW"] = new(
                    "P1-RECURVE-BOW",
                    cardNo: "SFD·016/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var recurveBowPrompt = ResolutionResult.BuildPrompts(recurveBowState)["P1"];
        var recurveBowCandidate = Assert.Single(
            recurveBowPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(recurveBowCandidate.Enabled);
        Assert.Equal(["P1-RECURVE-BOW"], (recurveBowCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_RED"], (recurveBowCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var recurveBowMetadata = Assert.IsType<Dictionary<string, object?>>(recurveBowCandidate.Metadata);
        var recurveBowRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(recurveBowMetadata["sourceRequirements"]));
        Assert.Equal("SFD·016/221", Assert.IsType<string>(recurveBowRequirement["equipmentCardNo"]));
        var recurveBowOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            recurveBowRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_RED"], recurveBowOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var arionsFallState = payableState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-ARIONS-FALL", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-ARIONS-FALL"] = new(
                    "P1-ARIONS-FALL",
                    cardNo: "SFD·030/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var arionsFallPrompt = ResolutionResult.BuildPrompts(arionsFallState)["P1"];
        var arionsFallCandidate = Assert.Single(
            arionsFallPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(arionsFallCandidate.Enabled);
        Assert.Equal(["P1-ARIONS-FALL"], (arionsFallCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_RED"], (arionsFallCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var arionsFallMetadata = Assert.IsType<Dictionary<string, object?>>(arionsFallCandidate.Metadata);
        var arionsFallRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(arionsFallMetadata["sourceRequirements"]));
        Assert.Equal("SFD·030/221", Assert.IsType<string>(arionsFallRequirement["equipmentCardNo"]));
        var arionsFallOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            arionsFallRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_RED"], arionsFallOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var witheredBattleaxeState = payableState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-WITHERED-BATTLEAXE", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-WITHERED-BATTLEAXE"] = new(
                    "P1-WITHERED-BATTLEAXE",
                    cardNo: "UNL-019/219",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var witheredBattleaxePrompt = ResolutionResult.BuildPrompts(witheredBattleaxeState)["P1"];
        var witheredBattleaxeCandidate = Assert.Single(
            witheredBattleaxePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(witheredBattleaxeCandidate.Enabled);
        Assert.Equal(["P1-WITHERED-BATTLEAXE"], (witheredBattleaxeCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_RED"], (witheredBattleaxeCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var witheredBattleaxeMetadata = Assert.IsType<Dictionary<string, object?>>(witheredBattleaxeCandidate.Metadata);
        var witheredBattleaxeRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(witheredBattleaxeMetadata["sourceRequirements"]));
        Assert.Equal("UNL-019/219", Assert.IsType<string>(witheredBattleaxeRequirement["equipmentCardNo"]));
        var witheredBattleaxeOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            witheredBattleaxeRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_RED"], witheredBattleaxeOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var clothArmorState = noPowerState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Blue] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-CLOTH-ARMOR", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CLOTH-ARMOR"] = new(
                    "P1-CLOTH-ARMOR",
                    cardNo: "SFD·064/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var clothArmorPrompt = ResolutionResult.BuildPrompts(clothArmorState)["P1"];
        var clothArmorCandidate = Assert.Single(
            clothArmorPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(clothArmorCandidate.Enabled);
        Assert.Equal(["P1-CLOTH-ARMOR"], (clothArmorCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["P1-UNIT"], (clothArmorCandidate.Targets ?? []).Select(target => target.Id).ToArray());
        Assert.Equal(["ASSEMBLE_BLUE"], (clothArmorCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());

        var clothArmorMetadata = Assert.IsType<Dictionary<string, object?>>(clothArmorCandidate.Metadata);
        var clothArmorRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(clothArmorMetadata["sourceRequirements"]));
        Assert.Equal("P1-CLOTH-ARMOR", Assert.IsType<string>(clothArmorRequirement["sourceObjectId"]));
        Assert.Equal("SFD·064/221", Assert.IsType<string>(clothArmorRequirement["equipmentCardNo"]));
        Assert.Equal(1, Assert.IsType<int>(clothArmorRequirement["powerCost"]));
        var clothArmorOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            clothArmorRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_BLUE"], clothArmorOptionalCostChoices.Select(cost => cost.Id).ToArray());
        var clothArmorRequiredOptionalCosts = Assert.IsAssignableFrom<IEnumerable<string>>(
            clothArmorRequirement["requiredOptionalCosts"]);
        Assert.Equal(["ASSEMBLE_BLUE"], clothArmorRequiredOptionalCosts.ToArray());

        var clothArmorRedOnlyState = clothArmorState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            }
        };
        var clothArmorRedOnlyPrompt = ResolutionResult.BuildPrompts(clothArmorRedOnlyState)["P1"];
        var clothArmorRedOnlyCandidate = Assert.Single(
            clothArmorRedOnlyPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.False(clothArmorRedOnlyCandidate.Enabled);
        Assert.Empty(clothArmorRedOnlyCandidate.Sources ?? []);

        var clothArmorRecyclePaymentState = clothArmorState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-CLOTH-ARMOR", "P1-UNIT", "P1-RUNE-BLUE-ASSEMBLE-PAYMENT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(clothArmorState.CardObjects, StringComparer.Ordinal)
            {
                ["P1-RUNE-BLUE-ASSEMBLE-PAYMENT"] = new(
                    "P1-RUNE-BLUE-ASSEMBLE-PAYMENT",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R03",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var clothArmorRecyclePrompt = ResolutionResult.BuildPrompts(clothArmorRecyclePaymentState)["P1"];
        var clothArmorRecycleCandidate = Assert.Single(
            clothArmorRecyclePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(clothArmorRecycleCandidate.Enabled);
        Assert.Equal(["P1-CLOTH-ARMOR"], (clothArmorRecycleCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        var clothArmorRecycleMetadata = Assert.IsType<Dictionary<string, object?>>(clothArmorRecycleCandidate.Metadata);
        var clothArmorRecycleRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(clothArmorRecycleMetadata["sourceRequirements"]));
        var clothArmorPaymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            clothArmorRecycleRequirement["paymentResourceChoices"]);
        Assert.Equal(["RECYCLE_RUNE:P1-RUNE-BLUE-ASSEMBLE-PAYMENT"], clothArmorPaymentResourceChoices.Select(choice => choice.Id).ToArray());
        var clothArmorPaymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            clothArmorRecycleRequirement["paymentResourcePowerByChoice"]);
        var clothArmorPaymentResourcePower = clothArmorPaymentResourcePowerByChoice["RECYCLE_RUNE:P1-RUNE-BLUE-ASSEMBLE-PAYMENT"];
        Assert.Equal(RuneTrait.Blue, Assert.IsType<string>(clothArmorPaymentResourcePower["trait"]));
        Assert.Equal(1, Assert.IsType<int>(clothArmorPaymentResourcePower["power"]));

        var hextechInfusedBulwarkState = clothArmorState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-HEXTECH-INFUSED-BULWARK", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HEXTECH-INFUSED-BULWARK"] = new(
                    "P1-HEXTECH-INFUSED-BULWARK",
                    cardNo: "SFD·073/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var hextechInfusedBulwarkPrompt = ResolutionResult.BuildPrompts(hextechInfusedBulwarkState)["P1"];
        var hextechInfusedBulwarkCandidate = Assert.Single(
            hextechInfusedBulwarkPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(hextechInfusedBulwarkCandidate.Enabled);
        Assert.Equal(["P1-HEXTECH-INFUSED-BULWARK"], (hextechInfusedBulwarkCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_BLUE"], (hextechInfusedBulwarkCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var hextechInfusedBulwarkMetadata = Assert.IsType<Dictionary<string, object?>>(hextechInfusedBulwarkCandidate.Metadata);
        var hextechInfusedBulwarkRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(hextechInfusedBulwarkMetadata["sourceRequirements"]));
        Assert.Equal("SFD·073/221", Assert.IsType<string>(hextechInfusedBulwarkRequirement["equipmentCardNo"]));
        var hextechInfusedBulwarkOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            hextechInfusedBulwarkRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_BLUE"], hextechInfusedBulwarkOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var wanderersGuidebookState = clothArmorState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-WANDERERS-GUIDEBOOK", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-WANDERERS-GUIDEBOOK"] = new(
                    "P1-WANDERERS-GUIDEBOOK",
                    cardNo: "SFD·086/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var wanderersGuidebookPrompt = ResolutionResult.BuildPrompts(wanderersGuidebookState)["P1"];
        var wanderersGuidebookCandidate = Assert.Single(
            wanderersGuidebookPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(wanderersGuidebookCandidate.Enabled);
        Assert.Equal(["P1-WANDERERS-GUIDEBOOK"], (wanderersGuidebookCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_BLUE"], (wanderersGuidebookCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var wanderersGuidebookMetadata = Assert.IsType<Dictionary<string, object?>>(wanderersGuidebookCandidate.Metadata);
        var wanderersGuidebookRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(wanderersGuidebookMetadata["sourceRequirements"]));
        Assert.Equal("SFD·086/221", Assert.IsType<string>(wanderersGuidebookRequirement["equipmentCardNo"]));
        var wanderersGuidebookOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            wanderersGuidebookRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_BLUE"], wanderersGuidebookOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var zDriveState = clothArmorState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-Z-DRIVE", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-Z-DRIVE"] = new(
                    "P1-Z-DRIVE",
                    cardNo: "SFD·090/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var zDrivePrompt = ResolutionResult.BuildPrompts(zDriveState)["P1"];
        var zDriveCandidate = Assert.Single(
            zDrivePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(zDriveCandidate.Enabled);
        Assert.Equal(["P1-Z-DRIVE"], (zDriveCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_BLUE"], (zDriveCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var zDriveMetadata = Assert.IsType<Dictionary<string, object?>>(zDriveCandidate.Metadata);
        var zDriveRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(zDriveMetadata["sourceRequirements"]));
        Assert.Equal("SFD·090/221", Assert.IsType<string>(zDriveRequirement["equipmentCardNo"]));
        var zDriveOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            zDriveRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_BLUE"], zDriveOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var steraksGageState = clothArmorState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Green] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-STERAKS-GAGE", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-STERAKS-GAGE"] = new(
                    "P1-STERAKS-GAGE",
                    cardNo: "SFD·056/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var steraksGagePrompt = ResolutionResult.BuildPrompts(steraksGageState)["P1"];
        var steraksGageCandidate = Assert.Single(
            steraksGagePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(steraksGageCandidate.Enabled);
        Assert.Equal(["P1-STERAKS-GAGE"], (steraksGageCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_GREEN"], (steraksGageCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var steraksGageMetadata = Assert.IsType<Dictionary<string, object?>>(steraksGageCandidate.Metadata);
        var steraksGageRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(steraksGageMetadata["sourceRequirements"]));
        Assert.Equal("SFD·056/221", Assert.IsType<string>(steraksGageRequirement["equipmentCardNo"]));
        var steraksGageOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            steraksGageRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_GREEN"], steraksGageOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var svarshangSongState = steraksGageState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-SVARSHANG-SONG", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SVARSHANG-SONG"] = new(
                    "P1-SVARSHANG-SONG",
                    cardNo: "SFD·059/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var svarshangSongPrompt = ResolutionResult.BuildPrompts(svarshangSongState)["P1"];
        var svarshangSongCandidate = Assert.Single(
            svarshangSongPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(svarshangSongCandidate.Enabled);
        Assert.Equal(["P1-SVARSHANG-SONG"], (svarshangSongCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_GREEN"], (svarshangSongCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var svarshangSongMetadata = Assert.IsType<Dictionary<string, object?>>(svarshangSongCandidate.Metadata);
        var svarshangSongRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(svarshangSongMetadata["sourceRequirements"]));
        Assert.Equal("SFD·059/221", Assert.IsType<string>(svarshangSongRequirement["equipmentCardNo"]));
        var svarshangSongOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            svarshangSongRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_GREEN"], svarshangSongOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var brutalizerState = steraksGageState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BRUTALIZER", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BRUTALIZER"] = new(
                    "P1-BRUTALIZER",
                    cardNo: "SFD·042/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var brutalizerPrompt = ResolutionResult.BuildPrompts(brutalizerState)["P1"];
        var brutalizerCandidate = Assert.Single(
            brutalizerPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(brutalizerCandidate.Enabled);
        Assert.Equal(["P1-BRUTALIZER"], (brutalizerCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_GREEN"], (brutalizerCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var brutalizerMetadata = Assert.IsType<Dictionary<string, object?>>(brutalizerCandidate.Metadata);
        var brutalizerRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(brutalizerMetadata["sourceRequirements"]));
        Assert.Equal("SFD·042/221", Assert.IsType<string>(brutalizerRequirement["equipmentCardNo"]));
        var brutalizerOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            brutalizerRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_GREEN"], brutalizerOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var guardianAngelState = steraksGageState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-GUARDIAN-ANGEL", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GUARDIAN-ANGEL"] = new(
                    "P1-GUARDIAN-ANGEL",
                    cardNo: "SFD·051/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var guardianAngelPrompt = ResolutionResult.BuildPrompts(guardianAngelState)["P1"];
        var guardianAngelCandidate = Assert.Single(
            guardianAngelPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(guardianAngelCandidate.Enabled);
        Assert.Equal(["P1-GUARDIAN-ANGEL"], (guardianAngelCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_GREEN"], (guardianAngelCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var guardianAngelMetadata = Assert.IsType<Dictionary<string, object?>>(guardianAngelCandidate.Metadata);
        var guardianAngelRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(guardianAngelMetadata["sourceRequirements"]));
        Assert.Equal("SFD·051/221", Assert.IsType<string>(guardianAngelRequirement["equipmentCardNo"]));
        var guardianAngelOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            guardianAngelRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_GREEN"], guardianAngelOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var soulSwordState = steraksGageState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-SOUL-SWORD", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SOUL-SWORD"] = new(
                    "P1-SOUL-SWORD",
                    cardNo: "UNL-039/219",
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var soulSwordPrompt = ResolutionResult.BuildPrompts(soulSwordState)["P1"];
        var soulSwordCandidate = Assert.Single(
            soulSwordPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(soulSwordCandidate.Enabled);
        Assert.Equal(["P1-SOUL-SWORD"], (soulSwordCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_GREEN"], (soulSwordCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var soulSwordMetadata = Assert.IsType<Dictionary<string, object?>>(soulSwordCandidate.Metadata);
        var soulSwordRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(soulSwordMetadata["sourceRequirements"]));
        Assert.Equal("UNL-039/219", Assert.IsType<string>(soulSwordRequirement["equipmentCardNo"]));
        var soulSwordOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            soulSwordRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_GREEN"], soulSwordOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var steraksGageRecyclePaymentState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-STERAKS-GAGE", "P1-UNIT", "P1-RUNE-GREEN-ASSEMBLE-PAYMENT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(steraksGageState.CardObjects, StringComparer.Ordinal)
            {
                ["P1-RUNE-GREEN-ASSEMBLE-PAYMENT"] = new(
                    "P1-RUNE-GREEN-ASSEMBLE-PAYMENT",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:green"],
                    cardNo: "UNL-R02",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var steraksGageRecyclePrompt = ResolutionResult.BuildPrompts(steraksGageRecyclePaymentState)["P1"];
        var steraksGageRecycleCandidate = Assert.Single(
            steraksGageRecyclePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(steraksGageRecycleCandidate.Enabled);
        var steraksGageRecycleMetadata = Assert.IsType<Dictionary<string, object?>>(steraksGageRecycleCandidate.Metadata);
        var steraksGageRecycleRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(steraksGageRecycleMetadata["sourceRequirements"]));
        var steraksGagePaymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            steraksGageRecycleRequirement["paymentResourceChoices"]);
        Assert.Equal(["RECYCLE_RUNE:P1-RUNE-GREEN-ASSEMBLE-PAYMENT"], steraksGagePaymentResourceChoices.Select(choice => choice.Id).ToArray());
        var steraksGagePaymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            steraksGageRecycleRequirement["paymentResourcePowerByChoice"]);
        var steraksGagePaymentResourcePower = steraksGagePaymentResourcePowerByChoice["RECYCLE_RUNE:P1-RUNE-GREEN-ASSEMBLE-PAYMENT"];
        Assert.Equal(RuneTrait.Green, Assert.IsType<string>(steraksGagePaymentResourcePower["trait"]));

        var doransShieldState = steraksGageState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-DORANS-SHIELD", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DORANS-SHIELD"] = new(
                    "P1-DORANS-SHIELD",
                    cardNo: "SFD·033/221",
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var doransShieldPrompt = ResolutionResult.BuildPrompts(doransShieldState)["P1"];
        var doransShieldCandidate = Assert.Single(
            doransShieldPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(doransShieldCandidate.Enabled);
        Assert.Equal(["P1-DORANS-SHIELD"], (doransShieldCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        var doransShieldMetadata = Assert.IsType<Dictionary<string, object?>>(doransShieldCandidate.Metadata);
        var doransShieldRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(doransShieldMetadata["sourceRequirements"]));
        Assert.Equal("SFD·033/221", Assert.IsType<string>(doransShieldRequirement["equipmentCardNo"]));
        var doransShieldOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            doransShieldRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_GREEN"], doransShieldOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var doransRingState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Purple] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-DORANS-RING", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DORANS-RING"] = new(
                    "P1-DORANS-RING",
                    cardNo: "SFD·124/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var doransRingPrompt = ResolutionResult.BuildPrompts(doransRingState)["P1"];
        var doransRingCandidate = Assert.Single(
            doransRingPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(doransRingCandidate.Enabled);
        Assert.Equal(["P1-DORANS-RING"], (doransRingCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_PURPLE"], (doransRingCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var doransRingMetadata = Assert.IsType<Dictionary<string, object?>>(doransRingCandidate.Metadata);
        var doransRingRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(doransRingMetadata["sourceRequirements"]));
        Assert.Equal("SFD·124/221", Assert.IsType<string>(doransRingRequirement["equipmentCardNo"]));
        var doransRingOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            doransRingRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_PURPLE"], doransRingOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var doransBladeState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-DORANS-BLADE", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DORANS-BLADE"] = new(
                    "P1-DORANS-BLADE",
                    cardNo: "SFD·095/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var doransBladePrompt = ResolutionResult.BuildPrompts(doransBladeState)["P1"];
        var doransBladeCandidate = Assert.Single(
            doransBladePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(doransBladeCandidate.Enabled);
        Assert.Equal(["P1-DORANS-BLADE"], (doransBladeCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_ORANGE"], (doransBladeCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var doransBladeMetadata = Assert.IsType<Dictionary<string, object?>>(doransBladeCandidate.Metadata);
        var doransBladeRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(doransBladeMetadata["sourceRequirements"]));
        Assert.Equal("SFD·095/221", Assert.IsType<string>(doransBladeRequirement["equipmentCardNo"]));
        var doransBladeOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            doransBladeRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_ORANGE"], doransBladeOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var hexdrinkerState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-HEXDRINKER", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HEXDRINKER"] = new(
                    "P1-HEXDRINKER",
                    cardNo: "SFD·102/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var hexdrinkerPrompt = ResolutionResult.BuildPrompts(hexdrinkerState)["P1"];
        var hexdrinkerCandidate = Assert.Single(
            hexdrinkerPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(hexdrinkerCandidate.Enabled);
        Assert.Equal(["P1-HEXDRINKER"], (hexdrinkerCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_ORANGE"], (hexdrinkerCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var hexdrinkerMetadata = Assert.IsType<Dictionary<string, object?>>(hexdrinkerCandidate.Metadata);
        var hexdrinkerRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(hexdrinkerMetadata["sourceRequirements"]));
        Assert.Equal("SFD·102/221", Assert.IsType<string>(hexdrinkerRequirement["equipmentCardNo"]));
        var hexdrinkerOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            hexdrinkerRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_ORANGE"], hexdrinkerOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var warmogsArmorState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-WARMOGS-ARMOR", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-WARMOGS-ARMOR"] = new(
                    "P1-WARMOGS-ARMOR",
                    cardNo: "SFD·108/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var warmogsArmorPrompt = ResolutionResult.BuildPrompts(warmogsArmorState)["P1"];
        var warmogsArmorCandidate = Assert.Single(
            warmogsArmorPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(warmogsArmorCandidate.Enabled);
        Assert.Equal(["P1-WARMOGS-ARMOR"], (warmogsArmorCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_ORANGE"], (warmogsArmorCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var warmogsArmorMetadata = Assert.IsType<Dictionary<string, object?>>(warmogsArmorCandidate.Metadata);
        var warmogsArmorRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(warmogsArmorMetadata["sourceRequirements"]));
        Assert.Equal("SFD·108/221", Assert.IsType<string>(warmogsArmorRequirement["equipmentCardNo"]));
        var warmogsArmorOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            warmogsArmorRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_ORANGE"], warmogsArmorOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var trinityForceState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-TRINITY-FORCE", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-TRINITY-FORCE"] = new(
                    "P1-TRINITY-FORCE",
                    cardNo: "SFD·115/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var trinityForcePrompt = ResolutionResult.BuildPrompts(trinityForceState)["P1"];
        var trinityForceCandidate = Assert.Single(
            trinityForcePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(trinityForceCandidate.Enabled);
        Assert.Equal(["P1-TRINITY-FORCE"], (trinityForceCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_ORANGE"], (trinityForceCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var trinityForceMetadata = Assert.IsType<Dictionary<string, object?>>(trinityForceCandidate.Metadata);
        var trinityForceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(trinityForceMetadata["sourceRequirements"]));
        Assert.Equal("SFD·115/221", Assert.IsType<string>(trinityForceRequirement["equipmentCardNo"]));
        var trinityForceOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            trinityForceRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_ORANGE"], trinityForceOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var huntersMacheteState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-HUNTERS-MACHETE", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HUNTERS-MACHETE"] = new(
                    "P1-HUNTERS-MACHETE",
                    cardNo: "UNL-096/219",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var huntersMachetePrompt = ResolutionResult.BuildPrompts(huntersMacheteState)["P1"];
        var huntersMacheteCandidate = Assert.Single(
            huntersMachetePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(huntersMacheteCandidate.Enabled);
        Assert.Equal(["P1-HUNTERS-MACHETE"], (huntersMacheteCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_ORANGE"], (huntersMacheteCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var huntersMacheteMetadata = Assert.IsType<Dictionary<string, object?>>(huntersMacheteCandidate.Metadata);
        var huntersMacheteRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(huntersMacheteMetadata["sourceRequirements"]));
        Assert.Equal("UNL-096/219", Assert.IsType<string>(huntersMacheteRequirement["equipmentCardNo"]));
        var huntersMacheteOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            huntersMacheteRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_ORANGE"], huntersMacheteOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var boneClubState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BONE-CLUB", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BONE-CLUB"] = new(
                    "P1-BONE-CLUB",
                    cardNo: "SFD·118/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var boneClubPrompt = ResolutionResult.BuildPrompts(boneClubState)["P1"];
        var boneClubCandidate = Assert.Single(
            boneClubPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(boneClubCandidate.Enabled);
        Assert.Equal(["P1-BONE-CLUB"], (boneClubCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_ORANGE"], (boneClubCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var boneClubMetadata = Assert.IsType<Dictionary<string, object?>>(boneClubCandidate.Metadata);
        var boneClubRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(boneClubMetadata["sourceRequirements"]));
        Assert.Equal("SFD·118/221", Assert.IsType<string>(boneClubRequirement["equipmentCardNo"]));
        var boneClubOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            boneClubRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_ORANGE"], boneClubOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var boneClubPromoState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Orange] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BONE-CLUB-PROMO", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BONE-CLUB-PROMO"] = new(
                    "P1-BONE-CLUB-PROMO",
                    cardNo: "SFD·118a/221·P",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var boneClubPromoPrompt = ResolutionResult.BuildPrompts(boneClubPromoState)["P1"];
        var boneClubPromoCandidate = Assert.Single(
            boneClubPromoPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(boneClubPromoCandidate.Enabled);
        Assert.Equal(["P1-BONE-CLUB-PROMO"], (boneClubPromoCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_ORANGE"], (boneClubPromoCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var boneClubPromoMetadata = Assert.IsType<Dictionary<string, object?>>(boneClubPromoCandidate.Metadata);
        var boneClubPromoRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(boneClubPromoMetadata["sourceRequirements"]));
        Assert.Equal("SFD·118a/221·P", Assert.IsType<string>(boneClubPromoRequirement["equipmentCardNo"]));
        var boneClubPromoOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            boneClubPromoRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_ORANGE"], boneClubPromoOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var bootsOfSwiftnessState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Purple] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BOOTS-OF-SWIFTNESS", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BOOTS-OF-SWIFTNESS"] = new(
                    "P1-BOOTS-OF-SWIFTNESS",
                    cardNo: "SFD·133/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var bootsOfSwiftnessPrompt = ResolutionResult.BuildPrompts(bootsOfSwiftnessState)["P1"];
        var bootsOfSwiftnessCandidate = Assert.Single(
            bootsOfSwiftnessPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(bootsOfSwiftnessCandidate.Enabled);
        Assert.Equal(["P1-BOOTS-OF-SWIFTNESS"], (bootsOfSwiftnessCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_PURPLE"], (bootsOfSwiftnessCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var bootsOfSwiftnessMetadata = Assert.IsType<Dictionary<string, object?>>(bootsOfSwiftnessCandidate.Metadata);
        var bootsOfSwiftnessRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(bootsOfSwiftnessMetadata["sourceRequirements"]));
        Assert.Equal("SFD·133/221", Assert.IsType<string>(bootsOfSwiftnessRequirement["equipmentCardNo"]));
        var bootsOfSwiftnessOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            bootsOfSwiftnessRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_PURPLE"], bootsOfSwiftnessOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var cullState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Purple] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-CULL", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CULL"] = new(
                    "P1-CULL",
                    cardNo: "SFD·134/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var cullPrompt = ResolutionResult.BuildPrompts(cullState)["P1"];
        var cullCandidate = Assert.Single(
            cullPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(cullCandidate.Enabled);
        Assert.Equal(["P1-CULL"], (cullCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_PURPLE"], (cullCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var cullMetadata = Assert.IsType<Dictionary<string, object?>>(cullCandidate.Metadata);
        var cullRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(cullMetadata["sourceRequirements"]));
        Assert.Equal("SFD·134/221", Assert.IsType<string>(cullRequirement["equipmentCardNo"]));
        var cullOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            cullRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_PURPLE"], cullOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var edgeOfNightState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Purple] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EDGE-OF-NIGHT", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EDGE-OF-NIGHT"] = new(
                    "P1-EDGE-OF-NIGHT",
                    cardNo: "SFD·139/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var edgeOfNightPrompt = ResolutionResult.BuildPrompts(edgeOfNightState)["P1"];
        var edgeOfNightCandidate = Assert.Single(
            edgeOfNightPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(edgeOfNightCandidate.Enabled);
        Assert.Equal(["P1-EDGE-OF-NIGHT"], (edgeOfNightCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_PURPLE"], (edgeOfNightCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var edgeOfNightMetadata = Assert.IsType<Dictionary<string, object?>>(edgeOfNightCandidate.Metadata);
        var edgeOfNightRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(edgeOfNightMetadata["sourceRequirements"]));
        Assert.Equal("SFD·139/221", Assert.IsType<string>(edgeOfNightRequirement["equipmentCardNo"]));
        var edgeOfNightOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            edgeOfNightRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_PURPLE"], edgeOfNightOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var vanguardsEyeState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Yellow] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-VANGUARDS-EYE", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-VANGUARDS-EYE"] = new(
                    "P1-VANGUARDS-EYE",
                    cardNo: "SFD·153/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var vanguardsEyePrompt = ResolutionResult.BuildPrompts(vanguardsEyeState)["P1"];
        var vanguardsEyeCandidate = Assert.Single(
            vanguardsEyePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(vanguardsEyeCandidate.Enabled);
        Assert.Equal(["P1-VANGUARDS-EYE"], (vanguardsEyeCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_YELLOW"], (vanguardsEyeCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var vanguardsEyeMetadata = Assert.IsType<Dictionary<string, object?>>(vanguardsEyeCandidate.Metadata);
        var vanguardsEyeRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(vanguardsEyeMetadata["sourceRequirements"]));
        Assert.Equal("SFD·153/221", Assert.IsType<string>(vanguardsEyeRequirement["equipmentCardNo"]));
        var vanguardsEyeOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            vanguardsEyeRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_YELLOW"], vanguardsEyeOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var bfSwordState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Yellow] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BF-SWORD", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BF-SWORD"] = new(
                    "P1-BF-SWORD",
                    cardNo: "SFD·161/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var bfSwordPrompt = ResolutionResult.BuildPrompts(bfSwordState)["P1"];
        var bfSwordCandidate = Assert.Single(
            bfSwordPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(bfSwordCandidate.Enabled);
        Assert.Equal(["P1-BF-SWORD"], (bfSwordCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_YELLOW"], (bfSwordCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var bfSwordMetadata = Assert.IsType<Dictionary<string, object?>>(bfSwordCandidate.Metadata);
        var bfSwordRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(bfSwordMetadata["sourceRequirements"]));
        Assert.Equal("SFD·161/221", Assert.IsType<string>(bfSwordRequirement["equipmentCardNo"]));
        var bfSwordOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            bfSwordRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_YELLOW"], bfSwordOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var sacredShearsState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Yellow] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-SACRED-SHEARS", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SACRED-SHEARS"] = new(
                    "P1-SACRED-SHEARS",
                    cardNo: "SFD·172/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var sacredShearsPrompt = ResolutionResult.BuildPrompts(sacredShearsState)["P1"];
        var sacredShearsCandidate = Assert.Single(
            sacredShearsPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(sacredShearsCandidate.Enabled);
        Assert.Equal(["P1-SACRED-SHEARS"], (sacredShearsCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_YELLOW"], (sacredShearsCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var sacredShearsMetadata = Assert.IsType<Dictionary<string, object?>>(sacredShearsCandidate.Metadata);
        var sacredShearsRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(sacredShearsMetadata["sourceRequirements"]));
        Assert.Equal("SFD·172/221", Assert.IsType<string>(sacredShearsRequirement["equipmentCardNo"]));
        var sacredShearsOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sacredShearsRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_YELLOW"], sacredShearsOptionalCostChoices.Select(cost => cost.Id).ToArray());

        var spinningAxeState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(0, 1),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-SPINNING-AXE", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPINNING-AXE"] = new(
                    "P1-SPINNING-AXE",
                    cardNo: "SFD·186/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便", "瞬息"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var spinningAxePrompt = ResolutionResult.BuildPrompts(spinningAxeState)["P1"];
        var spinningAxeCandidate = Assert.Single(
            spinningAxePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(spinningAxeCandidate.Enabled);
        Assert.Equal(["P1-SPINNING-AXE"], (spinningAxeCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_ANY_POWER"], (spinningAxeCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var spinningAxeMetadata = Assert.IsType<Dictionary<string, object?>>(spinningAxeCandidate.Metadata);
        var spinningAxeRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(spinningAxeMetadata["sourceRequirements"]));
        Assert.Equal("SFD·186/221", Assert.IsType<string>(spinningAxeRequirement["equipmentCardNo"]));
        var spinningAxeOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            spinningAxeRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_ANY_POWER"], spinningAxeOptionalCostChoices.Select(cost => cost.Id).ToArray());
        Assert.Equal(1, Assert.IsType<int>(spinningAxeRequirement["powerCost"]));

        var hearthfireCloakState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Blue] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-HEARTHFIRE-CLOAK", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HEARTHFIRE-CLOAK"] = new(
                    "P1-HEARTHFIRE-CLOAK",
                    cardNo: "SFD·190/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var hearthfireCloakPrompt = ResolutionResult.BuildPrompts(hearthfireCloakState)["P1"];
        var hearthfireCloakCandidate = Assert.Single(
            hearthfireCloakPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(hearthfireCloakCandidate.Enabled);
        Assert.Equal(["P1-HEARTHFIRE-CLOAK"], (hearthfireCloakCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_ANY_POWER"], (hearthfireCloakCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var hearthfireCloakMetadata = Assert.IsType<Dictionary<string, object?>>(hearthfireCloakCandidate.Metadata);
        var hearthfireCloakRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(hearthfireCloakMetadata["sourceRequirements"]));
        Assert.Equal("SFD·190/221", Assert.IsType<string>(hearthfireCloakRequirement["equipmentCardNo"]));
        var hearthfireCloakOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            hearthfireCloakRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_ANY_POWER"], hearthfireCloakOptionalCostChoices.Select(cost => cost.Id).ToArray());
        Assert.Equal(1, Assert.IsType<int>(hearthfireCloakRequirement["powerCost"]));

        var rabadonsDeathcapState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(0, 1),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-RABADONS-DEATHCAP", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-RABADONS-DEATHCAP"] = new(
                    "P1-RABADONS-DEATHCAP",
                    cardNo: "SFD·191/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var rabadonsDeathcapPrompt = ResolutionResult.BuildPrompts(rabadonsDeathcapState)["P1"];
        var rabadonsDeathcapCandidate = Assert.Single(
            rabadonsDeathcapPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(rabadonsDeathcapCandidate.Enabled);
        Assert.Equal(["P1-RABADONS-DEATHCAP"], (rabadonsDeathcapCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_ANY_POWER"], (rabadonsDeathcapCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var rabadonsDeathcapMetadata = Assert.IsType<Dictionary<string, object?>>(rabadonsDeathcapCandidate.Metadata);
        var rabadonsDeathcapRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(rabadonsDeathcapMetadata["sourceRequirements"]));
        Assert.Equal("SFD·191/221", Assert.IsType<string>(rabadonsDeathcapRequirement["equipmentCardNo"]));
        var rabadonsDeathcapOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            rabadonsDeathcapRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_ANY_POWER"], rabadonsDeathcapOptionalCostChoices.Select(cost => cost.Id).ToArray());
        Assert.Equal(1, Assert.IsType<int>(rabadonsDeathcapRequirement["powerCost"]));

        var shurelyasRequiemState = steraksGageState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Green] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-SHURELYAS-REQUIEM", "P1-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SHURELYAS-REQUIEM"] = new(
                    "P1-SHURELYAS-REQUIEM",
                    cardNo: "SFD·192/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT"] = new(
                    "P1-UNIT",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var shurelyasRequiemPrompt = ResolutionResult.BuildPrompts(shurelyasRequiemState)["P1"];
        var shurelyasRequiemCandidate = Assert.Single(
            shurelyasRequiemPrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(shurelyasRequiemCandidate.Enabled);
        Assert.Equal(["P1-SHURELYAS-REQUIEM"], (shurelyasRequiemCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["ASSEMBLE_ANY_POWER"], (shurelyasRequiemCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());
        var shurelyasRequiemMetadata = Assert.IsType<Dictionary<string, object?>>(shurelyasRequiemCandidate.Metadata);
        var shurelyasRequiemRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(shurelyasRequiemMetadata["sourceRequirements"]));
        Assert.Equal("SFD·192/221", Assert.IsType<string>(shurelyasRequiemRequirement["equipmentCardNo"]));
        var shurelyasRequiemOptionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            shurelyasRequiemRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_ANY_POWER"], shurelyasRequiemOptionalCostChoices.Select(cost => cost.Id).ToArray());
        Assert.Equal(1, Assert.IsType<int>(shurelyasRequiemRequirement["powerCost"]));

        var spinningAxeRecyclePaymentState = spinningAxeState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-SPINNING-AXE", "P1-UNIT", "P1-RUNE-RED-ASSEMBLE-PAYMENT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(spinningAxeState.CardObjects, StringComparer.Ordinal)
            {
                ["P1-RUNE-RED-ASSEMBLE-PAYMENT"] = new(
                    "P1-RUNE-RED-ASSEMBLE-PAYMENT",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var spinningAxeRecyclePrompt = ResolutionResult.BuildPrompts(spinningAxeRecyclePaymentState)["P1"];
        var spinningAxeRecycleCandidate = Assert.Single(
            spinningAxeRecyclePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        Assert.True(spinningAxeRecycleCandidate.Enabled);
        var spinningAxeRecycleMetadata = Assert.IsType<Dictionary<string, object?>>(spinningAxeRecycleCandidate.Metadata);
        var spinningAxeRecycleRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(spinningAxeRecycleMetadata["sourceRequirements"]));
        var spinningAxePaymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            spinningAxeRecycleRequirement["paymentResourceChoices"]);
        Assert.Equal(["RECYCLE_RUNE:P1-RUNE-RED-ASSEMBLE-PAYMENT"], spinningAxePaymentResourceChoices.Select(choice => choice.Id).ToArray());
        var spinningAxePaymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            spinningAxeRecycleRequirement["paymentResourcePowerByChoice"]);
        var spinningAxePaymentResourcePower = spinningAxePaymentResourcePowerByChoice["RECYCLE_RUNE:P1-RUNE-RED-ASSEMBLE-PAYMENT"];
        Assert.Equal(RuneTrait.Red, Assert.IsType<string>(spinningAxePaymentResourcePower["trait"]));
        Assert.Equal(1, Assert.IsType<int>(spinningAxePaymentResourcePower["power"]));
    }

    [Fact]
    public void ActionPromptHidesAssembleEquipmentSourceWhenEquipmentHasNoCardNo()
    {
        var state = new MatchState(
            "prompt-assemble-unknown-source-room",
            22,
            6,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE"] = new(
                    "P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var assembleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));

        Assert.False(assembleCandidate.Enabled);
        Assert.Empty(assembleCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(assembleCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
    }

    [Fact]
    public void ActionPromptHidesAssembleEquipmentTargetWhenUnitHasNoCardNo()
    {
        var state = new MatchState(
            "prompt-assemble-unknown-target-room",
            23,
            6,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-ASSEMBLE-TARGET-FILTER", "P1-UNIT-UNKNOWN-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-ASSEMBLE-TARGET-FILTER"] = new(
                    "P1-EQUIPMENT-ASSEMBLE-TARGET-FILTER",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-UNKNOWN-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-UNKNOWN-ASSEMBLE-TARGET",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var assembleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));

        Assert.False(assembleCandidate.Enabled);
        Assert.Empty(assembleCandidate.Sources ?? []);
        Assert.Empty(assembleCandidate.Targets ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(assembleCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
    }

    [Fact]
    public void ActionPromptHidesLegendActionSourceWhenLegendHasNoCardNo()
    {
        var state = new MatchState(
            "prompt-legend-unknown-source-room",
            23,
            6,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-LEGEND-ACTION-TARGET", "P1-EQUIPMENT-LEGEND-ACTION-ARMAMENT"],
                    Battlefields = ["P1-BATTLEFIELD-PORO-FORGE"],
                    LegendZone = ["P1-LEGEND-UNKNOWN-ACTION-SOURCE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-UNKNOWN-ACTION-SOURCE"] = new(
                    "P1-LEGEND-UNKNOWN-ACTION-SOURCE",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-PORO-FORGE"] = new(
                    "P1-BATTLEFIELD-PORO-FORGE",
                    cardNo: "SFD·208/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-LEGEND-ACTION-TARGET"] = new(
                    "P1-UNIT-LEGEND-ACTION-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-EQUIPMENT-LEGEND-ACTION-ARMAMENT"] = new(
                    "P1-EQUIPMENT-LEGEND-ACTION-ARMAMENT",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var legendCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "LEGEND_ACT", StringComparison.Ordinal));

        Assert.False(legendCandidate.Enabled);
        Assert.Empty(legendCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(legendCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
    }

    [Fact]
    public void ActionPromptHidesLegendActionSourceWhenRequiredBattlefieldIsOpponentOwned()
    {
        var state = new MatchState(
            "prompt-legend-dirty-required-battlefield-room",
            23,
            6,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-LEGEND-ACTION-TARGET", "P1-EQUIPMENT-LEGEND-ACTION-ARMAMENT"],
                    Battlefields = ["P1-BATTLEFIELD-PORO-FORGE"],
                    LegendZone = ["P1-LEGEND-PORO-FORGE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-PORO-FORGE"] = new(
                    "P1-LEGEND-PORO-FORGE",
                    cardNo: "UNL-237/219",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-PORO-FORGE"] = new(
                    "P1-BATTLEFIELD-PORO-FORGE",
                    cardNo: "SFD·208/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: ""),
                ["P1-UNIT-LEGEND-ACTION-TARGET"] = new(
                    "P1-UNIT-LEGEND-ACTION-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-EQUIPMENT-LEGEND-ACTION-ARMAMENT"] = new(
                    "P1-EQUIPMENT-LEGEND-ACTION-ARMAMENT",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var legendCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "LEGEND_ACT", StringComparison.Ordinal));

        Assert.False(legendCandidate.Enabled);
        Assert.Empty(legendCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(legendCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
    }

    [Fact]
    public void ActionPromptHidesLegendActionTargetWhenUnitHasNoCardNo()
    {
        var state = new MatchState(
            "prompt-legend-unknown-target-room",
            23,
            6,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
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
                    Battlefields = ["P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET"],
                    LegendZone = ["P1-LEGEND-YASUO-TARGET-FILTER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-YASUO-TARGET-FILTER"] = new(
                    "P1-LEGEND-YASUO-TARGET-FILTER",
                    cardNo: "FND-259/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET"] = new(
                    "P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var legendCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "LEGEND_ACT", StringComparison.Ordinal));

        Assert.False(legendCandidate.Enabled);
        Assert.Empty(legendCandidate.Sources ?? []);
        Assert.Empty(legendCandidate.Targets ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(legendCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
    }

    [Fact]
    public void ActionPromptHidesActivateAbilitySourceWhenGrantedUnitHasNoCardNo()
    {
        var state = new MatchState(
            "prompt-activate-unknown-source-room",
            24,
            6,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-MUTATION-GARDEN", "P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MUTATION-GARDEN"] = new(
                    "P1-BATTLEFIELD-MUTATION-GARDEN",
                    cardNo: "UNL-213/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE"] = new(
                    "P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var abilityCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));

        Assert.False(abilityCandidate.Enabled);
        Assert.Empty(abilityCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(abilityCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
    }

    [Fact]
    public void ActionPromptHidesActivateAbilitySourceWhenGrantingBattlefieldIsOpponentOwned()
    {
        var state = new MatchState(
            "prompt-activate-dirty-granting-battlefield-room",
            24,
            6,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-MUTATION-GARDEN", "P1-BATTLEFIELD-EXPERIENCE-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MUTATION-GARDEN"] = new(
                    "P1-BATTLEFIELD-MUTATION-GARDEN",
                    cardNo: "UNL-213/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: ""),
                ["P1-BATTLEFIELD-EXPERIENCE-UNIT"] = new(
                    "P1-BATTLEFIELD-EXPERIENCE-UNIT",
                    cardNo: "SFD·001/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var abilityCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));

        Assert.False(abilityCandidate.Enabled);
        Assert.Empty(abilityCandidate.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(abilityCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
    }

    [Fact]
    public void ActionPromptHidesActivateAbilityTargetWhenUnitHasNoCardNo()
    {
        var state = new MatchState(
            "prompt-activate-unknown-target-room",
            25,
            6,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(0, 1),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-XERATH-TARGET-FILTER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH-TARGET-FILTER"] = new(
                    "P1-UNIT-XERATH-TARGET-FILTER",
                    cardNo: "UNL-026/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET"] = new(
                    "P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var abilityCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));

        Assert.True(abilityCandidate.Enabled);
        Assert.Equal(
            ["P1-UNIT-XERATH-TARGET-FILTER"],
            (abilityCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(
            ["P1-UNIT-XERATH-TARGET-FILTER"],
            (abilityCandidate.Targets ?? []).Select(target => target.Id).ToArray());
        var metadata = Assert.IsType<Dictionary<string, object?>>(abilityCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();
        var xerathRequirement = Assert.Single(sourceRequirements);
        Assert.Equal("P1-UNIT-XERATH-TARGET-FILTER", Assert.IsType<string>(xerathRequirement["sourceObjectId"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            xerathRequirement["targetChoicesByIndex"]);
        Assert.Equal(
            ["P1-UNIT-XERATH-TARGET-FILTER"],
            targetChoicesByIndex["0"].Select(choice => choice.Id).ToArray());
    }

    [Fact]
    public void ActionPromptActivateAbilityMetadataFiltersSourcesTargetsAndSpellshieldTax()
    {
        var noResourceState = new MatchState(
            "prompt-activate-ability-room",
            17,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-VI", "P1-UNIT-VI-OPPONENT-CONTROLLED"],
                    Battlefields = ["P1-UNIT-XERATH", "P1-UNIT-XERATH-OPPONENT-CONTROLLED"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT", "P2-SPELLSHIELD-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new(
                    "P1-UNIT-VI",
                    cardNo: "UNL-030/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-VI-OPPONENT-CONTROLLED"] = new(
                    "P1-UNIT-VI-OPPONENT-CONTROLLED",
                    cardNo: "UNL-030/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-UNIT-XERATH"] = new(
                    "P1-UNIT-XERATH",
                    cardNo: "UNL-026/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-XERATH-OPPONENT-CONTROLLED"] = new(
                    "P1-UNIT-XERATH-OPPONENT-CONTROLLED",
                    cardNo: "UNL-026/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-UNIT"] = new(
                    "P2-UNIT",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-SPELLSHIELD-UNIT"] = new(
                    "P2-SPELLSHIELD-UNIT",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    ownerId: "P2",
                    controllerId: "P2")
            });

        var noResourcePrompt = ResolutionResult.BuildPrompts(noResourceState)["P1"];
        var noResourceCandidate = Assert.Single(
            noResourcePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
        Assert.False(noResourceCandidate.Enabled);
        Assert.Empty(noResourceCandidate.Sources ?? []);

        var payableState = noResourceState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 1),
                ["P2"] = RunePool.Empty
            }
        };
        var payablePrompt = ResolutionResult.BuildPrompts(payableState)["P1"];
        var payableCandidate = Assert.Single(
            payablePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
        Assert.True(payableCandidate.Enabled);
        Assert.Equal(["P1-UNIT-VI", "P1-UNIT-XERATH"], (payableCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(
            ["PAY_2_RED_DOUBLE_POWER", "PAY_RED_EXHAUST_DAMAGE_3"],
            (payableCandidate.Modes ?? []).Select(mode => mode.Id).ToArray());
        var payableTargetIds = (payableCandidate.Targets ?? []).Select(target => target.Id).ToArray();
        Assert.Contains("P1-UNIT-VI", payableTargetIds);
        Assert.Contains("P1-UNIT-XERATH", payableTargetIds);
        Assert.Contains("P2-UNIT", payableTargetIds);
        Assert.Contains("P2-SPELLSHIELD-UNIT", payableTargetIds);

        var metadata = Assert.IsType<Dictionary<string, object?>>(payableCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();
        Assert.Equal(2, sourceRequirements.Length);
        var viRequirement = Assert.Single(sourceRequirements, requirement =>
            string.Equals(Assert.IsType<string>(requirement["abilityId"]), "PAY_2_RED_DOUBLE_POWER", StringComparison.Ordinal));
        Assert.Equal("P1-UNIT-VI", Assert.IsType<string>(viRequirement["sourceObjectId"]));
        Assert.Equal(2, Assert.IsType<int>(viRequirement["manaCost"]));
        Assert.Equal(1, Assert.IsType<int>(viRequirement["powerCost"]));
        Assert.Equal(0, Assert.IsType<int>(viRequirement["minTargetCount"]));
        Assert.Equal(0, Assert.IsType<int>(viRequirement["maxTargetCount"]));
        Assert.True(Assert.IsType<bool>(viRequirement["composable"]));

        var xerathRequirement = Assert.Single(sourceRequirements, requirement =>
            string.Equals(Assert.IsType<string>(requirement["abilityId"]), "PAY_RED_EXHAUST_DAMAGE_3", StringComparison.Ordinal));
        Assert.Equal("P1-UNIT-XERATH", Assert.IsType<string>(xerathRequirement["sourceObjectId"]));
        Assert.Equal(0, Assert.IsType<int>(xerathRequirement["manaCost"]));
        Assert.Equal(1, Assert.IsType<int>(xerathRequirement["powerCost"]));
        Assert.Equal(1, Assert.IsType<int>(xerathRequirement["minTargetCount"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            xerathRequirement["targetChoicesByIndex"]);
        var xerathTargetIds = targetChoicesByIndex["0"].Select(choice => choice.Id).ToArray();
        Assert.Contains("P1-UNIT-VI", xerathTargetIds);
        Assert.Contains("P1-UNIT-XERATH", xerathTargetIds);
        Assert.Contains("P2-UNIT", xerathTargetIds);
        Assert.Contains("P2-SPELLSHIELD-UNIT", xerathTargetIds);

        var noSpellshieldTaxManaState = noResourceState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(0, 1),
                ["P2"] = RunePool.Empty
            }
        };
        var noSpellshieldTaxManaCandidate = Assert.Single(
            ResolutionResult.BuildPrompts(noSpellshieldTaxManaState)["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
        var noTaxMetadata = Assert.IsType<Dictionary<string, object?>>(noSpellshieldTaxManaCandidate.Metadata);
        var noTaxRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            noTaxMetadata["sourceRequirements"]);
        var noTaxXerath = Assert.Single(noTaxRequirements, requirement =>
            string.Equals(Assert.IsType<string>(requirement["abilityId"]), "PAY_RED_EXHAUST_DAMAGE_3", StringComparison.Ordinal));
        var noTaxTargetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            noTaxXerath["targetChoicesByIndex"]);
        var noTaxTargetIds = noTaxTargetChoicesByIndex["0"].Select(choice => choice.Id).ToArray();
        Assert.Contains("P1-UNIT-VI", noTaxTargetIds);
        Assert.Contains("P1-UNIT-XERATH", noTaxTargetIds);
        Assert.Contains("P2-UNIT", noTaxTargetIds);
        Assert.DoesNotContain("P2-SPELLSHIELD-UNIT", noTaxTargetIds);
    }

    [Fact]
    public void ActionPromptActivateAbilityMetadataUsesLegacyOwnedSources()
    {
        var state = new MatchState(
            "prompt-activate-ability-legacy-room",
            17,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 1),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-VI-LEGACY", "P1-UNIT-VI-DIRTY-P2"],
                    Battlefields = ["P1-UNIT-XERATH-LEGACY", "P1-UNIT-XERATH-DIRTY-P2"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI-LEGACY"] = new(
                    "P1-UNIT-VI-LEGACY",
                    cardNo: "UNL-030/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: ""),
                ["P1-UNIT-VI-DIRTY-P2"] = new(
                    "P1-UNIT-VI-DIRTY-P2",
                    cardNo: "UNL-030/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: ""),
                ["P1-UNIT-XERATH-LEGACY"] = new(
                    "P1-UNIT-XERATH-LEGACY",
                    cardNo: "UNL-026/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: ""),
                ["P1-UNIT-XERATH-DIRTY-P2"] = new(
                    "P1-UNIT-XERATH-DIRTY-P2",
                    cardNo: "UNL-026/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: ""),
                ["P2-UNIT"] = new(
                    "P2-UNIT",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));

        Assert.True(candidate.Enabled);
        Assert.Equal(
            ["P1-UNIT-VI-LEGACY", "P1-UNIT-XERATH-LEGACY"],
            (candidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.DoesNotContain(candidate.Sources ?? [], source => string.Equals(source.Id, "P1-UNIT-VI-DIRTY-P2", StringComparison.Ordinal));
        Assert.DoesNotContain(candidate.Sources ?? [], source => string.Equals(source.Id, "P1-UNIT-XERATH-DIRTY-P2", StringComparison.Ordinal));

        var metadata = Assert.IsType<Dictionary<string, object?>>(candidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();
        Assert.Equal(
            ["P1-UNIT-VI-LEGACY", "P1-UNIT-XERATH-LEGACY"],
            sourceRequirements.Select(requirement => Assert.IsType<string>(requirement["sourceObjectId"])).ToArray());
    }

    [Fact]
    public void ActionPromptLegendActMetadataFiltersSourcesAbilitiesTargetsAndCosts()
    {
        var noResourceState = new MatchState(
            "prompt-legend-action-room",
            18,
            3,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BASE-UNIT", "P1-ARMAMENT"],
                    Battlefields = ["P1-BATTLEFIELD-UNIT"],
                    LegendZone = ["P1-LEGEND-YASUO", "P1-LEGEND-POPPY", "P1-LEGEND-JAX", "P1-LEGEND-OPPONENT-OWNED"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-YASUO"] = new(
                    "P1-LEGEND-YASUO",
                    cardNo: "FND-259/298",
                    tags: ["CARD_TYPE:LEGEND"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-POPPY"] = new(
                    "P1-LEGEND-POPPY",
                    cardNo: "UNL-237/219",
                    tags: ["CARD_TYPE:LEGEND"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-JAX"] = new(
                    "P1-LEGEND-JAX",
                    cardNo: "SFD·193/221",
                    tags: ["CARD_TYPE:LEGEND"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-OPPONENT-OWNED"] = new(
                    "P1-LEGEND-OPPONENT-OWNED",
                    cardNo: "FND-259/298",
                    tags: ["CARD_TYPE:LEGEND"],
                    ownerId: "P2"),
                ["P1-BASE-UNIT"] = new(
                    "P1-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-UNIT"] = new(
                    "P1-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-ARMAMENT"] = new(
                    "P1-ARMAMENT",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var noResourcePrompt = ResolutionResult.BuildPrompts(noResourceState)["P1"];
        var noResourceCandidate = Assert.Single(
            noResourcePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "LEGEND_ACT", StringComparison.Ordinal));
        Assert.False(noResourceCandidate.Enabled);
        Assert.Empty(noResourceCandidate.Sources ?? []);

        var payableState = noResourceState with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 3,
                ["P2"] = 0
            }
        };
        var payablePrompt = ResolutionResult.BuildPrompts(payableState)["P1"];
        var payableCandidate = Assert.Single(
            payablePrompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "LEGEND_ACT", StringComparison.Ordinal));

        Assert.True(payableCandidate.Enabled);
        Assert.Equal(
            ["P1-LEGEND-YASUO", "P1-LEGEND-POPPY", "P1-LEGEND-JAX"],
            (payableCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(
            [
                "LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT",
                "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
                "LEGEND_PAY_1_EXHAUST_ATTACH_UNATTACHED_ARMAMENT"
            ],
            (payableCandidate.Modes ?? []).Select(mode => mode.Id).ToArray());
        Assert.Equal(
            ["P1-BASE-UNIT", "P1-BATTLEFIELD-UNIT", "P1-ARMAMENT"],
            (payableCandidate.Targets ?? []).Select(target => target.Id).ToArray());
        Assert.Equal(
            ["SPEND_MANA:2", "SPEND_EXPERIENCE:3", "SPEND_MANA:1"],
            (payableCandidate.OptionalCosts ?? []).Select(cost => cost.Id).ToArray());

        var metadata = Assert.IsType<Dictionary<string, object?>>(payableCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();
        Assert.Equal(3, sourceRequirements.Length);

        var yasuoRequirement = Assert.Single(sourceRequirements, requirement =>
            string.Equals(Assert.IsType<string>(requirement["abilityId"]), "LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-YASUO", Assert.IsType<string>(yasuoRequirement["sourceObjectId"]));
        Assert.Equal(2, Assert.IsType<int>(yasuoRequirement["manaCost"]));
        Assert.Equal(0, Assert.IsType<int>(yasuoRequirement["experienceCost"]));
        Assert.Equal(1, Assert.IsType<int>(yasuoRequirement["minTargetCount"]));
        Assert.Equal("主阶段开环", Assert.IsType<string>(yasuoRequirement["timingLabel"]));
        Assert.True(Assert.IsType<bool>(yasuoRequirement["composable"]));
        var yasuoTargetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            yasuoRequirement["targetChoicesByIndex"]);
        Assert.Equal(
            ["P1-BASE-UNIT", "P1-BATTLEFIELD-UNIT"],
            yasuoTargetChoicesByIndex["0"].Select(choice => choice.Id).ToArray());
        var yasuoRequiredCosts = Assert.IsAssignableFrom<IEnumerable<string>>(yasuoRequirement["requiredOptionalCosts"]);
        Assert.Equal(["SPEND_MANA:2"], yasuoRequiredCosts.ToArray());

        var poppyRequirement = Assert.Single(sourceRequirements, requirement =>
            string.Equals(Assert.IsType<string>(requirement["abilityId"]), "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-POPPY", Assert.IsType<string>(poppyRequirement["sourceObjectId"]));
        Assert.Equal(0, Assert.IsType<int>(poppyRequirement["manaCost"]));
        Assert.Equal(3, Assert.IsType<int>(poppyRequirement["experienceCost"]));
        Assert.Equal(0, Assert.IsType<int>(poppyRequirement["minTargetCount"]));
        Assert.True(Assert.IsType<bool>(poppyRequirement["composable"]));
        var poppyRequiredCosts = Assert.IsAssignableFrom<IEnumerable<string>>(poppyRequirement["requiredOptionalCosts"]);
        Assert.Equal(["SPEND_EXPERIENCE:3"], poppyRequiredCosts.ToArray());

        var jaxRequirement = Assert.Single(sourceRequirements, requirement =>
            string.Equals(Assert.IsType<string>(requirement["abilityId"]), "LEGEND_PAY_1_EXHAUST_ATTACH_UNATTACHED_ARMAMENT", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-JAX", Assert.IsType<string>(jaxRequirement["sourceObjectId"]));
        Assert.False(Assert.IsType<bool>(jaxRequirement["composable"]));
        Assert.Contains("第二目标", Assert.IsType<string>(jaxRequirement["unsupportedReason"]));
        var jaxTargetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            jaxRequirement["targetChoicesByIndex"]);
        Assert.Equal(["P1-BASE-UNIT", "P1-BATTLEFIELD-UNIT"], jaxTargetChoicesByIndex["0"].Select(choice => choice.Id).ToArray());
        Assert.Equal(["P1-ARMAMENT"], jaxTargetChoicesByIndex["1"].Select(choice => choice.Id).ToArray());
    }

    [Fact]
    public void MatchStateExposesTurnWindowSpellDuelAndBattleViews()
    {
        var state = new MatchState(
            "turn-window-room",
            13,
            3,
            "bob",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            timingState: TimingStates.NeutralClosed,
            priorityPlayerId: "bob",
            stackItems:
            [
                new StackItemState(
                    "STACK-1",
                    "alice",
                    "A-SPELL-1",
                    "DAMAGE",
                    "OGN·009/298",
                    ["B-DEFENDER-1"],
                    timingContext: TimingStates.SpellDuelOpen)
            ],
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "A-ATTACKER-1"]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = ["B-DEFENDER-1"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("BF-1", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "alice", controllerId: "alice"),
                ["A-ATTACKER-1"] = new(
                    "A-ATTACKER-1",
                    power: 3,
                    isAttacking: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "alice",
                    controllerId: "alice"),
                ["B-DEFENDER-1"] = new(
                    "B-DEFENDER-1",
                    power: 2,
                    isDefending: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "bob",
                    controllerId: "bob")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["A-ATTACKER-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["B-DEFENDER-1"] = new("bob", "BATTLEFIELD", "BF-1")
            });

        Assert.Equal(TimingStates.SpellDuelClosed, state.TurnWindow.State);
        Assert.True(state.TurnWindow.IsSpellDuel);
        Assert.True(state.SpellDuelState.IsActive);
        Assert.True(state.SpellDuelState.IsClosed);
        Assert.Equal(["STACK-1"], state.SpellDuelState.StackItemIds);
        Assert.True(state.BattleState.IsActive);
        Assert.Equal("BF-1", state.BattleState.BattlefieldObjectId);
        Assert.Equal(["A-ATTACKER-1"], state.BattleState.AttackerObjectIds);
        Assert.Equal(["B-DEFENDER-1"], state.BattleState.DefenderObjectIds);

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var turnWindow = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["turnWindow"]);
        var spellDuel = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["spellDuel"]);
        var battle = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["battle"]);
        Assert.Equal(TimingStates.SpellDuelClosed, Assert.IsType<string>(turnWindow["state"]));
        Assert.True(Assert.IsType<bool>(spellDuel["isActive"]));
        Assert.Equal(["A-ATTACKER-1"], StringList(battle["attackerObjectIds"]));
    }

    [Fact]
    public void MatchStateBattleStateUsesLegacyOwnedParticipantControllers()
    {
        var state = new MatchState(
            "battle-legacy-controller-room",
            14,
            3,
            "bob",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            timingState: TimingStates.NeutralClosed,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "A-ATTACKER-LEGACY"]
                },
                ["bob"] = PlayerZones.Empty with
                {
                    Battlefields = ["B-DEFENDER-LEGACY"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new(
                    "BF-1",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "alice",
                    controllerId: "alice"),
                ["A-ATTACKER-LEGACY"] = new(
                    "A-ATTACKER-LEGACY",
                    power: 3,
                    isAttacking: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "alice"),
                ["B-DEFENDER-LEGACY"] = new(
                    "B-DEFENDER-LEGACY",
                    power: 2,
                    isDefending: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "bob")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["A-ATTACKER-LEGACY"] = new("alice", "BATTLEFIELD", "BF-1"),
                ["B-DEFENDER-LEGACY"] = new("bob", "BATTLEFIELD", "BF-1")
            });

        Assert.True(state.BattleState.IsActive);
        Assert.Equal("BF-1", state.BattleState.BattlefieldObjectId);
        Assert.Equal("alice", state.BattleState.ParticipantControllerIds["A-ATTACKER-LEGACY"]);
        Assert.Equal("bob", state.BattleState.ParticipantControllerIds["B-DEFENDER-LEGACY"]);

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var battle = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["battle"]);
        var participantControllerIds = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(battle["participantControllerIds"]);
        Assert.Equal("alice", participantControllerIds["A-ATTACKER-LEGACY"]);
        Assert.Equal("bob", participantControllerIds["B-DEFENDER-LEGACY"]);
    }

    [Fact]
    public void MatchStateExposesContinuousEffectPowerLayerViews()
    {
        var state = new MatchState(
            "continuous-effect-room",
            14,
            3,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            untilEndOfTurnEffects: ["GLOBAL_DAMAGE_PREVENTION"],
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty with
                {
                    Base = ["A-UNIT-1"]
                },
                ["bob"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["A-UNIT-1"] = new(
                    "A-UNIT-1",
                    untilEndOfTurnEffects: ["PREVENT_NEXT_DAMAGE"],
                    power: 5,
                    untilEndOfTurnPowerModifier: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "alice",
                    controllerId: "alice")
            });

        var powerEffect = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal));
        Assert.Equal("A-UNIT-1", powerEffect.TargetObjectId);
        Assert.Equal(2, powerEffect.PowerDelta);
        Assert.Equal(3, powerEffect.BasePower);
        Assert.Equal(5, powerEffect.EffectivePower);
        Assert.Contains(
            state.ContinuousEffects,
            effect => string.Equals(effect.EffectId, "GLOBAL:GLOBAL_DAMAGE_PREVENTION", StringComparison.Ordinal));

        var snapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var continuousEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            snapshot.Timing["continuousEffects"]);
        Assert.Contains(
            continuousEffects,
            effect => string.Equals(Assert.IsType<string>(effect["layer"]), ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(Assert.IsType<string>(effect["targetObjectId"]), "A-UNIT-1", StringComparison.Ordinal)
                && Assert.IsType<int>(effect["basePower"]) == 3
                && Assert.IsType<int>(effect["effectivePower"]) == 5);

        var objects = ObjectView(PlayerView(snapshot, "alice"));
        var unitView = Assert.IsType<Dictionary<string, object?>>(objects["A-UNIT-1"]);
        Assert.Equal(3, Assert.IsType<int>(unitView["basePower"]));
        Assert.Equal(5, Assert.IsType<int>(unitView["effectivePower"]));
        Assert.Equal(5, Assert.IsType<int>(unitView["power"]));
    }

    [Fact]
    public void SnapshotsRedactOpponentFaceDownObjects()
    {
        var state = new MatchState(
            "dev-room",
            7,
            2,
            "alice",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["alice", "bob"],
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["alice"] = PlayerZones.Empty,
                ["bob"] = PlayerZones.Empty with
                {
                    Base = ["B-FACEDOWN-STANDBY-1"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["B-FACEDOWN-STANDBY-1"] = new(
                    "B-FACEDOWN-STANDBY-1",
                    isFaceDown: true,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    manaCost: 1)
            });

        var aliceSnapshot = ResolutionResult.BuildSnapshots(state)["alice"];
        var aliceBobObjects = ObjectView(PlayerView(aliceSnapshot, "bob"));
        var aliceHiddenObject = Assert.IsType<Dictionary<string, object?>>(aliceBobObjects["B-FACEDOWN-STANDBY-1"]);
        Assert.Equal("B-FACEDOWN-STANDBY-1", Assert.IsType<string>(aliceHiddenObject["objectId"]));
        Assert.True(Assert.IsType<bool>(aliceHiddenObject["isFaceDown"]));
        Assert.DoesNotContain("power", aliceHiddenObject.Keys);
        Assert.DoesNotContain("tags", aliceHiddenObject.Keys);
        Assert.DoesNotContain("manaCost", aliceHiddenObject.Keys);

        var bobSnapshot = ResolutionResult.BuildSnapshots(state)["bob"];
        var bobObjects = ObjectView(PlayerView(bobSnapshot, "bob"));
        var bobOwnObject = Assert.IsType<Dictionary<string, object?>>(bobObjects["B-FACEDOWN-STANDBY-1"]);
        Assert.Equal(2, Assert.IsType<int>(bobOwnObject["power"]));
        Assert.Contains(CardObjectTags.Standby, StringList(bobOwnObject["tags"]));
        Assert.Equal(1, Assert.IsType<int>(bobOwnObject["manaCost"]));
    }

    [Fact]
    public async Task SeedScenarioCreatesPlayableDevelopmentState()
    {
        var session = new MatchSession("dev-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var seed = await session.SeedScenarioAsync(
            "P1",
            "seed-basic-play",
            "basic-play",
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = "basic-play" }),
            CancellationToken.None);

        var p1View = PlayerView(seed.Snapshots["P1"], "P1");
        var p1Zones = ZoneView(p1View);

        Assert.True(seed.Accepted);
        Assert.Contains(seed.Events, evt => string.Equals(evt.Kind, "DEV_SCENARIO_SEEDED", StringComparison.Ordinal));
        Assert.Contains("P1-UNIT-MIGHTY-FAERIE", StringList(p1Zones["hand"]));
        Assert.Contains("PLAY_CARD", seed.Prompts["P1"].Actions);

        var play = await session.SubmitAsync(
            "P1",
            "play-mighty-faerie",
            new PlayCardCommand("P1-UNIT-MIGHTY-FAERIE", "SFD·125/221", []),
            JsonSerializer.SerializeToElement(new
            {
                cmdType = "PLAY_CARD",
                sourceObjectId = "P1-UNIT-MIGHTY-FAERIE",
                cardNo = "SFD·125/221",
                targetObjectIds = Array.Empty<string>()
            }),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.Contains(play.Events, evt => string.Equals(evt.Kind, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Contains("PASS_PRIORITY", play.Prompts["P1"].Actions);
    }

    [Fact]
    public async Task SeedScenarioCreatesTwoPlayerTestDecks()
    {
        var session = new MatchSession("dev-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var seed = await session.SeedScenarioAsync(
            "P1",
            "seed-test-decks",
            "test-decks",
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = "test-decks" }),
            CancellationToken.None);

        Assert.True(seed.Accepted);

        var p1View = PlayerView(seed.Snapshots["P1"], "P1");
        var p1Zones = ZoneView(p1View);
        Assert.Equal(12, Assert.IsType<int>(p1Zones["mainDeckCount"]));
        Assert.Equal(8, Assert.IsType<int>(p1Zones["runeDeckCount"]));
        Assert.Equal(5, StringList(p1Zones["hand"]).Count);
        Assert.Contains("P1-UNIT-MIGHTY-FAERIE", StringList(p1Zones["hand"]));
        Assert.Contains("P1-LEGEND-POPPY", StringList(p1Zones["legendZone"]));
        Assert.Contains("P1-CHAMPION-001", StringList(p1Zones["championZone"]));

        var p2OwnView = PlayerView(seed.Snapshots["P2"], "P2");
        var p2OwnZones = ZoneView(p2OwnView);
        Assert.Equal(12, Assert.IsType<int>(p2OwnZones["mainDeckCount"]));
        Assert.Equal(8, Assert.IsType<int>(p2OwnZones["runeDeckCount"]));
        Assert.Equal(5, StringList(p2OwnZones["hand"]).Count);
        Assert.Contains("P2-SPELL-HEXTECH-RAY", StringList(p2OwnZones["hand"]));
        Assert.Contains("P2-LEGEND-YASUO", StringList(p2OwnZones["legendZone"]));
        Assert.Contains("P2-CHAMPION-001", StringList(p2OwnZones["championZone"]));

        var p2FromP1Zones = ZoneView(PlayerView(seed.Snapshots["P1"], "P2"));
        Assert.Empty(StringList(p2FromP1Zones["hand"]));
        Assert.Equal(5, Assert.IsType<int>(p2FromP1Zones["handHidden"]));

        var p1Objects = ObjectView(p1View);
        var p1HandObject = Assert.IsType<Dictionary<string, object?>>(p1Objects["P1-UNIT-MIGHTY-FAERIE"]);
        Assert.Equal("SFD·125/221", Assert.IsType<string>(p1HandObject["cardNo"]));

        var p2Objects = ObjectView(p2OwnView);
        var p2HandObject = Assert.IsType<Dictionary<string, object?>>(p2Objects["P2-SPELL-HEXTECH-RAY"]);
        Assert.Equal("OGN·009/298", Assert.IsType<string>(p2HandObject["cardNo"]));

        var playCandidate = Assert.Single(
            seed.Prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.Contains(
            playCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-UNIT-MIGHTY-FAERIE", StringComparison.Ordinal));
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

    private static async Task ReadyBothAsync(MatchSession session)
    {
        await ReadyBothAsync(session, "P1", "P2");
    }

    private static async Task ReadyBothAsync(MatchSession session, string firstPlayerId, string secondPlayerId)
    {
        await session.ReadyAsync(firstPlayerId, $"ready-{firstPlayerId}", RawCommand("READY"), CancellationToken.None);
        await session.ReadyAsync(secondPlayerId, $"ready-{secondPlayerId}", RawCommand("READY"), CancellationToken.None);
    }

    private static string PlayerSeat(SnapshotDto snapshot, string playerId)
    {
        var player = Assert.IsType<Dictionary<string, object?>>(snapshot.Players[playerId]);
        return Assert.IsType<string>(player["seat"]);
    }

    private static Dictionary<string, object?> PlayerView(SnapshotDto snapshot, string playerId)
    {
        return Assert.IsType<Dictionary<string, object?>>(snapshot.Players[playerId]);
    }

    private static Dictionary<string, object?> ZoneView(Dictionary<string, object?> player)
    {
        return Assert.IsType<Dictionary<string, object?>>(player["zones"]);
    }

    private static Dictionary<string, object?> ObjectView(Dictionary<string, object?> player)
    {
        return Assert.IsType<Dictionary<string, object?>>(player["objects"]);
    }

    private static IReadOnlyList<string> StringList(object? value)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(value);
    }

    private static JsonElement RawCommand(string cmdType)
    {
        return JsonDocument.Parse($$"""{"cmdType":"{{cmdType}}"}""").RootElement.Clone();
    }
}
