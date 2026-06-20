using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LocalPlayabilityRuleRegressionTests
{
    [Fact]
    public async Task PlayCardUnitToPreciseBattlefieldStartsSpellDuelAfterStackResolution()
    {
        var engine = new CoreRuleEngine();
        var state = PlayUnitToContestedBattlefieldState();

        var played = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-play-unit-to-precise-battlefield", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-HAND-UNIT",
                "SFD·125/221",
                [],
                Destination: "BATTLEFIELD:BF-1"),
            CancellationToken.None);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(TimingStates.NeutralClosed, played.State.TimingState);
        Assert.Equal(new ObjectLocationState("P1", "STACK"), played.State.ObjectLocations["P1-HAND-UNIT"]);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-play-unit-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-play-unit-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(TimingStates.SpellDuelOpen, p2Pass.State.TimingState);
        Assert.Equal("P1", p2Pass.State.FocusPlayerId);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF-1"), p2Pass.State.ObjectLocations["P1-HAND-UNIT"]);
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal));
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            p2Pass.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
    }

    [Fact]
    public void PlayCardPromptUsesRealBattlefieldDestinationsForUnits()
    {
        var state = PlayUnitToContestedBattlefieldState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");

        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var requirement = Assert.Single(
            requirements,
            item => string.Equals(item["sourceObjectId"] as string, "P1-HAND-UNIT", StringComparison.Ordinal));
        var destinationChoices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(
            requirement["destinationChoices"]);

        Assert.Contains(destinationChoices, choice => string.Equals(choice.Id, "BASE", StringComparison.Ordinal));
        Assert.Contains(destinationChoices, choice => string.Equals(choice.Id, "BATTLEFIELD:BF-1", StringComparison.Ordinal));
        Assert.DoesNotContain(destinationChoices, choice => string.Equals(choice.Id, "BATTLEFIELD:P1-MAIN", StringComparison.Ordinal));
    }

    [Fact]
    public void ActionPromptChoicesProjectExplicitObjectIdsForFrontendInteraction()
    {
        var state = PlayUnitToContestedBattlefieldState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));

        var sourceChoice = Assert.Single(
            playCandidate.Sources ?? [],
            choice => string.Equals(choice.Id, "P1-HAND-UNIT", StringComparison.Ordinal));
        Assert.Equal(["P1-HAND-UNIT"], sourceChoice.ObjectIds);

        var destinationChoice = Assert.Single(
            playCandidate.Destinations ?? [],
            choice => string.Equals(choice.Id, "BATTLEFIELD:BF-1", StringComparison.Ordinal));
        Assert.Equal(["BATTLEFIELD:BF-1", "BF-1"], destinationChoice.ObjectIds);

        var sourceStep = Assert.Single(
            playCandidate.SelectionSteps ?? [],
            step => string.Equals(step.Role, "source", StringComparison.Ordinal));
        var sourceStepChoice = Assert.Single(
            sourceStep.Choices,
            choice => string.Equals(choice.Id, "P1-HAND-UNIT", StringComparison.Ordinal));
        Assert.Equal(sourceChoice.ObjectIds, sourceStepChoice.ObjectIds);

        Assert.True(sourceStep.Required);
        Assert.Equal("来源", sourceStep.Label);

        var destinationStep = Assert.Single(
            playCandidate.SelectionSteps ?? [],
            step => string.Equals(step.Role, "destination", StringComparison.Ordinal));
        Assert.False(destinationStep.Required);
        Assert.Equal("位置", destinationStep.Label);
        Assert.Contains(destinationStep.Choices, choice =>
            string.Equals(choice.Id, "BASE", StringComparison.Ordinal)
            && choice.ObjectIds.SequenceEqual(["BASE"]));
        Assert.Contains(destinationStep.Choices, choice =>
            string.Equals(choice.Id, "BATTLEFIELD:BF-1", StringComparison.Ordinal)
            && choice.ObjectIds.SequenceEqual(["BATTLEFIELD:BF-1", "BF-1"]));
    }

    [Fact]
    public void ActionPromptObjectContextsExposeSelectionRolesAndCommandFieldsForFrontend()
    {
        var state = PlayUnitToContestedBattlefieldState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        var promptInspection = Assert.IsType<ActionPromptInspectionDto>(prompt.Inspection);
        Assert.Equal("server-action-prompt", promptInspection.Source);
        Assert.Contains("隐藏 metadata", promptInspection.Boundary);
        Assert.Contains(promptInspection.SummaryRows, row =>
            string.Equals(row.Key, "candidate", StringComparison.Ordinal)
            && row.Value.Contains("可提交", StringComparison.Ordinal)
            && row.Value.Contains("阻断", StringComparison.Ordinal));
        Assert.Contains(promptInspection.Groups, group =>
            string.Equals(group.Key, "candidate", StringComparison.Ordinal)
            && group.Rows.Any(row => row.Value.Contains(CommandTypes.PlayCard, StringComparison.Ordinal)));
        Assert.Contains(promptInspection.Groups, group =>
            string.Equals(group.Key, "safe-boundary", StringComparison.Ordinal)
            && group.Rows.Any(row => row.Value.Contains("展示与提交", StringComparison.Ordinal)));
        var objectContexts = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptObjectContextDto>>(prompt.ObjectContexts);

        var sourceContext = Assert.Single(
            objectContexts,
            context => string.Equals(context.ObjectId, "P1-HAND-UNIT", StringComparison.Ordinal));
        Assert.Equal(ActionPromptContextSources.ServerActionPrompt, sourceContext.Source);
        Assert.Contains("隐藏 metadata", sourceContext.Boundary);
        var sourceCandidate = Assert.Single(
            sourceContext.Candidates,
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.Equal(["来源"], sourceCandidate.Roles);
        Assert.Equal(CommandTypes.PlayCard, sourceCandidate.CommandType);
        Assert.True(sourceCandidate.Composer?.Supported);
        Assert.Contains("服务端", sourceCandidate.Composer?.Reason ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("source", sourceCandidate.Composer?.SelectionRoles ?? []);
        Assert.Contains("sourceObjectId", sourceCandidate.Composer?.CommandFields ?? []);
        Assert.Contains("来源:sourceObjectId*", sourceCandidate.RequiredCommandFields ?? []);
        Assert.Contains("位置:destination", sourceCandidate.CommandFields ?? []);
        var sourceInspection = Assert.IsType<ActionPromptObjectInspectionDto>(sourceContext.Inspection);
        Assert.Equal("server-action-prompt", sourceInspection.Source);
        Assert.Contains("隐藏 metadata", sourceInspection.Boundary);
        Assert.Contains(sourceInspection.SummaryRows, row =>
            string.Equals(row.Key, "candidate", StringComparison.Ordinal)
            && string.Equals(row.Value, "1 可提交 / 0 阻断", StringComparison.Ordinal));
        Assert.Contains(sourceInspection.Groups, group =>
            string.Equals(group.Key, "candidate", StringComparison.Ordinal)
            && group.Rows.Any(row =>
                row.Value.Contains(CommandTypes.PlayCard, StringComparison.Ordinal)
                && row.Value.Contains("组合 服务端声明", StringComparison.Ordinal)));
        Assert.Contains(sourceInspection.Groups, group =>
            string.Equals(group.Key, "command-fields", StringComparison.Ordinal)
            && group.Rows.Any(row => row.Value.Contains("sourceObjectId", StringComparison.Ordinal)));
        Assert.Contains(sourceInspection.Groups, group =>
            string.Equals(group.Key, "safe-boundary", StringComparison.Ordinal)
            && group.Rows.Any(row => row.Value.Contains("前端不重算", StringComparison.Ordinal)));

        var battlefieldContext = Assert.Single(
            objectContexts,
            context => string.Equals(context.ObjectId, "BF-1", StringComparison.Ordinal));
        Assert.Equal(ActionPromptContextSources.ServerActionPrompt, battlefieldContext.Source);
        Assert.Contains("对象候选", battlefieldContext.Boundary);
        var battlefieldCandidate = Assert.Single(
            battlefieldContext.Candidates,
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.Equal(["位置"], battlefieldCandidate.Roles);
        Assert.Equal(CommandTypes.PlayCard, battlefieldCandidate.CommandType);
        Assert.True(battlefieldCandidate.Composer?.Supported);
    }

    [Fact]
    public void SnapshotExposesAuthoritativeTableLayoutPartitionsForFrontend()
    {
        var state = FrontendTableLayoutSnapshotState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var snapshot = session.SnapshotFor("P1");
        var p1Zones = ZoneView(PlayerView(snapshot, "P1"));
        var p2Zones = ZoneView(PlayerView(snapshot, "P2"));

        Assert.Equal(["P1-BASE-CARD"], StringList(p1Zones["baseCards"]));
        Assert.Equal(["P1-RUNE-READY"], StringList(p1Zones["baseRunes"]));
        Assert.Equal(["P2-BASE-CARD"], StringList(p2Zones["baseCards"]));
        Assert.Equal(["P2-RUNE-READY"], StringList(p2Zones["baseRunes"]));

        var battlefields = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Lanes["battlefields"]);
        var battlefield = Assert.Single(
            battlefields,
            item => string.Equals(item["battlefieldObjectId"] as string, "BF-LAYOUT", StringComparison.Ordinal));
        Assert.Equal(["P1-LAYOUT-UNIT", "P2-LAYOUT-UNIT"], StringList(battlefield["occupantObjectIds"]));

        var unitsBySide = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            battlefield["unitsBySide"]);
        Assert.Equal(["P1-LAYOUT-UNIT"], unitsBySide["P1"]);
        Assert.Equal(["P2-LAYOUT-UNIT"], unitsBySide["P2"]);
    }

    [Fact]
    public void ActionPromptCandidatesProvideServerCommandTemplateForFrontendComposer()
    {
        var state = PlayUnitToContestedBattlefieldState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));

        var template = Assert.IsType<ActionPromptCommandTemplateDto>(playCandidate.CommandTemplate);
        Assert.Equal(CommandTypes.PlayCard, template.CmdType);
        Assert.Contains(template.Bindings, binding =>
            string.Equals(binding.Field, "sourceObjectId", StringComparison.Ordinal)
            && string.Equals(binding.Source, "selectedSource", StringComparison.Ordinal)
            && binding.Required
            && string.Equals(binding.Label, "来源", StringComparison.Ordinal));
        Assert.Contains(template.Bindings, binding =>
            string.Equals(binding.Field, "cardNo", StringComparison.Ordinal)
            && string.Equals(binding.Source, "requirementMetadata", StringComparison.Ordinal)
            && binding.Required
            && string.Equals(binding.Label, "服务端", StringComparison.Ordinal)
            && binding.MetadataKeys is not null
            && binding.MetadataKeys.Contains("cardNo", StringComparer.Ordinal));
        Assert.Contains(template.Bindings, binding =>
            string.Equals(binding.Field, "targetObjectIds", StringComparison.Ordinal)
            && string.Equals(binding.Source, "selectedTargets", StringComparison.Ordinal)
            && binding.AsArray
            && !binding.OmitEmpty);
        Assert.Contains(template.Bindings, binding =>
            string.Equals(binding.Field, "destination", StringComparison.Ordinal)
            && string.Equals(binding.Source, "selectedDestination", StringComparison.Ordinal));

        var tapRuneCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal));
        var tapRuneTemplate = Assert.IsType<ActionPromptCommandTemplateDto>(tapRuneCandidate.CommandTemplate);
        Assert.Equal(CommandTypes.TapRune, tapRuneTemplate.CmdType);
        Assert.Contains(tapRuneTemplate.Bindings, binding =>
            string.Equals(binding.Field, "sourceObjectId", StringComparison.Ordinal)
            && string.Equals(binding.Source, "selectedSource", StringComparison.Ordinal)
            && binding.Required
            && string.Equals(binding.Label, "来源", StringComparison.Ordinal));

        var recycleRuneCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.RecycleRune, StringComparison.Ordinal));
        var recycleRuneTemplate = Assert.IsType<ActionPromptCommandTemplateDto>(recycleRuneCandidate.CommandTemplate);
        Assert.Equal(CommandTypes.RecycleRune, recycleRuneTemplate.CmdType);
        Assert.Contains(recycleRuneTemplate.Bindings, binding =>
            string.Equals(binding.Field, "sourceObjectId", StringComparison.Ordinal)
            && string.Equals(binding.Source, "selectedSource", StringComparison.Ordinal)
            && binding.Required
            && string.Equals(binding.Label, "来源", StringComparison.Ordinal));

        var endTurnCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.EndTurn, StringComparison.Ordinal));
        var endTurnTemplate = Assert.IsType<ActionPromptCommandTemplateDto>(endTurnCandidate.CommandTemplate);
        Assert.Equal(CommandTypes.EndTurn, endTurnTemplate.CmdType);
        Assert.Empty(endTurnTemplate.Bindings);

        var surrenderCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.Surrender, StringComparison.Ordinal));
        var surrenderTemplate = Assert.IsType<ActionPromptCommandTemplateDto>(surrenderCandidate.CommandTemplate);
        Assert.Equal(CommandTypes.Surrender, surrenderTemplate.CmdType);
        Assert.Empty(surrenderTemplate.Bindings);
    }

    [Fact]
    public async Task DeclareBattleConquestScoresAndTriggersConquestForNonHuntUnit()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            BattleStateForConquest(),
            new PlayerIntent("intent-non-hunt-conquest-score", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                "BF-1",
                ["P1-ATTACKER"],
                ["P2-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.PlayerScores["P1"]);
        Assert.Equal("P1", result.State.CardObjects["BF-1"].ControllerId);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        var scoreEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        Assert.Equal("P1", scoreEvent.Payload["playerId"]);
        Assert.Equal(1, scoreEvent.Payload["amount"]);
        Assert.Equal("BATTLEFIELD_CONQUERED_SCORE", scoreEvent.Payload["reason"]);
        Assert.Contains(
            BattlefieldTaskMarkers.ScoreGainedThisTurn("BF-1", "P1"),
            result.State.UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task TurnStartHeldBattlefieldScoresAndTriggersOgn275Minion()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            TurnStartHeldOgn275State(),
            new PlayerIntent("intent-end-turn-into-held-score", "P2", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Equal(1, result.State.PlayerScores["P1"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_CREATE_MINION", StringComparison.Ordinal));
        var scoreEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal));
        Assert.Equal("BATTLEFIELD_HELD_SCORE", scoreEvent.Payload["reason"]);
        var tokenEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, "BATTLEFIELD_HELD_CREATE_MINION", StringComparison.Ordinal));
        var tokenObjectId = Assert.IsType<string>(tokenEvent.Payload["tokenObjectId"]);
        Assert.Contains(tokenObjectId, result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task CatalogCostSpecDoesNotExposePrintedUnitPowerAsPowerCost()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var unit = RuleTextParser.Parse(Card(catalog, "UNL-022/219"));
        Assert.Equal(4, unit.Cost.Mana);
        Assert.Equal(1, unit.Cost.ReturnEnergy);
        Assert.Null(unit.Cost.Power);

        var spell = RuleTextParser.Parse(Card(catalog, "OGN·004/298"));
        Assert.Equal(1, spell.Cost.Mana);
        Assert.Null(spell.Cost.Power);
    }

    [Fact]
    public async Task MoveUnitToOpponentControlledEmptyBattlefieldStartsNonBattleSpellDuel()
    {
        var result = await new CoreRuleEngine().ResolveAsync(
            MoveUnitToOpponentControlledEmptyBattlefieldState(),
            new PlayerIntent("intent-move-into-empty-controlled-battlefield", "P2", CommandTypes.MoveUnit),
            new MoveUnitCommand("P2-BASE-MOVER", "BASE", "BATTLEFIELD:BF-1", []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P2", result.State.FocusPlayerId);
        Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "BF-1"), result.State.ObjectLocations["P2-BASE-MOVER"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal));
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.DoesNotContain(result.State.PendingTaskQueue.Tasks, task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NonBattleSpellDuelPassesResolveControlAndConquestScore()
    {
        var engine = new CoreRuleEngine();
        var moved = await engine.ResolveAsync(
            MoveUnitToOpponentControlledEmptyBattlefieldState(),
            new PlayerIntent("intent-move-into-empty-controlled-battlefield-flow", "P2", CommandTypes.MoveUnit),
            new MoveUnitCommand("P2-BASE-MOVER", "BASE", "BATTLEFIELD:BF-1", []),
            CancellationToken.None);
        Assert.True(moved.Accepted, moved.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            moved.State,
            new PlayerIntent("intent-non-battle-spell-duel-p2-pass", "P2", CommandTypes.PassFocus),
            new PassFocusCommand(),
            CancellationToken.None);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-non-battle-spell-duel-p1-pass", "P1", CommandTypes.PassFocus),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.Equal(TimingStates.NeutralOpen, p1Pass.State.TimingState);
        Assert.Equal("P2", p1Pass.State.CardObjects["BF-1"].ControllerId);
        Assert.Equal(1, p1Pass.State.PlayerScores["P2"]);
        Assert.Contains(p1Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "SPELL_DUEL_CLOSED", StringComparison.Ordinal));
        Assert.Contains(p1Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(p1Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.Contains(p1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_CONQUERED_SCORE", StringComparison.Ordinal));
        Assert.Empty(p1Pass.State.PendingTaskQueue.Tasks);
    }

    private static MatchState PlayUnitToContestedBattlefieldState()
    {
        return new MatchState(
            roomId: "local-playability-play-unit-battlefield",
            tick: 0,
            turnNumber: 3,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(10, 10),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P2-DEFENDER"]
                }
            },
            playerScores: Scores(),
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P2", "OGN·275/298"),
                ["P1-HAND-UNIT"] = Unit("P1-HAND-UNIT", "P1", power: 2),
                ["P2-DEFENDER"] = Unit("P2-DEFENDER", "P2", power: 2)
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P2", "BATTLEFIELD", "BF-1"),
                ["P1-HAND-UNIT"] = new("P1", "HAND"),
                ["P2-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-1")
            });
    }

    private static MatchState FrontendTableLayoutSnapshotState()
    {
        return new MatchState(
            roomId: "local-playability-frontend-table-layout",
            tick: 7,
            turnNumber: 3,
            activePlayerId: "P1",
            seats: Seats(),
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
                    Base = ["P1-BASE-CARD", "P1-RUNE-READY"],
                    Battlefields = ["BF-LAYOUT", "P1-LAYOUT-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-CARD", "P2-RUNE-READY"],
                    Battlefields = ["P2-LAYOUT-UNIT"]
                }
            },
            playerScores: Scores(),
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-LAYOUT"] = Battlefield("BF-LAYOUT", "P1", "OGN·275/298"),
                ["P1-BASE-CARD"] = Unit("P1-BASE-CARD", "P1", power: 2),
                ["P1-RUNE-READY"] = Rune("P1-RUNE-READY", "P1"),
                ["P1-LAYOUT-UNIT"] = Unit("P1-LAYOUT-UNIT", "P1", power: 2),
                ["P2-BASE-CARD"] = Unit("P2-BASE-CARD", "P2", power: 2),
                ["P2-RUNE-READY"] = Rune("P2-RUNE-READY", "P2"),
                ["P2-LAYOUT-UNIT"] = Unit("P2-LAYOUT-UNIT", "P2", power: 2)
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-LAYOUT"] = new("P1", "BATTLEFIELD", "BF-LAYOUT"),
                ["P1-BASE-CARD"] = new("P1", "BASE"),
                ["P1-RUNE-READY"] = new("P1", "BASE"),
                ["P1-LAYOUT-UNIT"] = new("P1", "BATTLEFIELD", "BF-LAYOUT"),
                ["P2-BASE-CARD"] = new("P2", "BASE"),
                ["P2-RUNE-READY"] = new("P2", "BASE"),
                ["P2-LAYOUT-UNIT"] = new("P2", "BATTLEFIELD", "BF-LAYOUT")
            });
    }

    private static MatchState BattleStateForConquest()
    {
        return new MatchState(
            roomId: "local-playability-conquest-score",
            tick: 0,
            turnNumber: 3,
            activePlayerId: "P1",
            seats: Seats(),
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
                    Battlefields = ["P1-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P2-DEFENDER"]
                }
            },
            playerScores: Scores(),
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P2", "OGN·276/298"),
                ["P1-ATTACKER"] = Unit("P1-ATTACKER", "P1", power: 4),
                ["P2-DEFENDER"] = Unit("P2-DEFENDER", "P2", power: 1)
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P2", "BATTLEFIELD", "BF-1"),
                ["P1-ATTACKER"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P2-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-1")
            },
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("BF-1")]);
    }

    private static MatchState TurnStartHeldOgn275State()
    {
        return new MatchState(
            roomId: "local-playability-held-score-ogn275",
            tick: 0,
            turnNumber: 2,
            activePlayerId: "P2",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P2",
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
                    MainDeck = ["P1-DRAW"],
                    Battlefields = ["BF-1", "P1-HOLDER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            playerScores: Scores(),
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P1", "OGN·275/298"),
                ["P1-HOLDER"] = Unit("P1-HOLDER", "P1", power: 2, exhausted: true),
                ["P1-DRAW"] = new(
                    "P1-DRAW",
                    cardNo: "OGN·004/298",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-HOLDER"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-DRAW"] = new("P1", "MAIN_DECK")
            });
    }

    private static MatchState MoveUnitToOpponentControlledEmptyBattlefieldState()
    {
        return new MatchState(
            roomId: "local-playability-non-battle-spell-duel-conquest",
            tick: 0,
            turnNumber: 3,
            activePlayerId: "P2",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P2",
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
                    Battlefields = ["BF-1"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-MOVER"],
                    MainDeck = ["P2-DRAW"]
                }
            },
            playerScores: Scores(),
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P1", "OGN·275/298"),
                ["P2-BASE-MOVER"] = Unit("P2-BASE-MOVER", "P2", power: 2),
                ["P2-DRAW"] = new(
                    "P2-DRAW",
                    cardNo: "OGN·004/298",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P2-BASE-MOVER"] = new("P2", "BASE"),
                ["P2-DRAW"] = new("P2", "MAIN_DECK")
            });
    }

    private static Dictionary<string, string> Seats()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["P1"] = "connection-1",
            ["P2"] = "connection-2"
        };
    }

    private static Dictionary<string, int> Scores()
    {
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["P1"] = 0,
            ["P2"] = 0
        };
    }

    private static CardObjectState Battlefield(string objectId, string playerId, string cardNo)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int power,
        bool exhausted = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·125/221",
            power: power,
            isExhausted: exhausted,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Rune(string objectId, string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·001/221",
            tags: [CardObjectTags.RuneCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static OfficialCard Card(OfficialCardCatalog catalog, string cardNo)
    {
        return catalog.Cards.Single(card => string.Equals(card.CardNo, cardNo, StringComparison.Ordinal));
    }

    private static Dictionary<string, object?> PlayerView(SnapshotDto snapshot, string playerId)
    {
        return Assert.IsType<Dictionary<string, object?>>(snapshot.Players[playerId]);
    }

    private static Dictionary<string, object?> ZoneView(Dictionary<string, object?> player)
    {
        return Assert.IsType<Dictionary<string, object?>>(player["zones"]);
    }

    private static IReadOnlyList<string> StringList(object? value)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(value);
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
