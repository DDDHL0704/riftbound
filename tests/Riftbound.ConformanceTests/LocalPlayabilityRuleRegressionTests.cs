using System.Text.Json;
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
        var serverFlow = Assert.IsType<ActionPromptServerFlowDto>(prompt.ServerFlow);

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
        var sourcePresentation = Assert.IsType<ActionPromptCandidatePresentationDto>(sourceCandidate.Presentation);
        Assert.Equal("play", sourcePresentation.Category);
        Assert.Equal("play-card", sourcePresentation.Intent);
        Assert.Equal("card-action", sourcePresentation.UiHint);
        Assert.Equal(100, sourcePresentation.Priority);
        Assert.True(sourceCandidate.Composer?.Supported);
        Assert.Contains("服务端", sourceCandidate.Composer?.Reason ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("source", sourceCandidate.Composer?.SelectionRoles ?? []);
        Assert.Contains("sourceObjectId", sourceCandidate.Composer?.CommandFields ?? []);
        Assert.Contains("来源:sourceObjectId*", sourceCandidate.RequiredCommandFields ?? []);
        Assert.Contains("位置:destination", sourceCandidate.CommandFields ?? []);
        var sourceCandidateSteps = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptObjectCandidateStepDto>>(
            sourceCandidate.SelectionSteps);
        var sourceCandidateSourceStep = Assert.Single(
            sourceCandidateSteps,
            step => string.Equals(step.Role, "source", StringComparison.Ordinal));
        Assert.Equal("来源", sourceCandidateSourceStep.Label);
        Assert.True(sourceCandidateSourceStep.Required);
        Assert.Equal(1, sourceCandidateSourceStep.ChoiceCount);
        Assert.Equal(1, sourceCandidateSourceStep.ObjectChoiceCount);
        var sourceCandidateDestinationStep = Assert.Single(
            sourceCandidateSteps,
            step => string.Equals(step.Role, "destination", StringComparison.Ordinal));
        Assert.Equal("位置", sourceCandidateDestinationStep.Label);
        Assert.False(sourceCandidateDestinationStep.Required);
        Assert.True(sourceCandidateDestinationStep.ChoiceCount >= 2);
        Assert.Equal(0, sourceCandidateDestinationStep.ObjectChoiceCount);
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
            string.Equals(group.Key, "selection-steps", StringComparison.Ordinal)
            && group.Rows.Any(row =>
                row.Value.Contains("来源", StringComparison.Ordinal)
                && row.Value.Contains("1/1", StringComparison.Ordinal)));
        Assert.Contains(sourceInspection.Groups, group =>
            string.Equals(group.Key, "safe-boundary", StringComparison.Ordinal)
            && group.Rows.Any(row => row.Value.Contains("前端不重算", StringComparison.Ordinal)));
        var sourceServerFlowRef = Assert.Single(
            Assert.IsAssignableFrom<IReadOnlyList<ActionPromptServerFlowObjectRefDto>>(serverFlow.RelatedObjects),
            context => string.Equals(context.ObjectId, "P1-HAND-UNIT", StringComparison.Ordinal)
                && context.CandidateActions is not null);
        Assert.Contains(CommandTypes.PlayCard, sourceServerFlowRef.CandidateActions!);

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
        var battlefieldPresentation = Assert.IsType<ActionPromptCandidatePresentationDto>(battlefieldCandidate.Presentation);
        Assert.Equal("play", battlefieldPresentation.Category);
        Assert.Equal("play-card", battlefieldPresentation.Intent);
        Assert.Equal("card-action", battlefieldPresentation.UiHint);
        Assert.Equal(100, battlefieldPresentation.Priority);
        Assert.True(battlefieldCandidate.Composer?.Supported);
        var battlefieldCandidateSteps = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptObjectCandidateStepDto>>(
            battlefieldCandidate.SelectionSteps);
        var battlefieldDestinationStep = Assert.Single(
            battlefieldCandidateSteps,
            step => string.Equals(step.Role, "destination", StringComparison.Ordinal));
        Assert.Equal("位置", battlefieldDestinationStep.Label);
        Assert.False(battlefieldDestinationStep.Required);
        Assert.True(battlefieldDestinationStep.ChoiceCount >= 2);
        Assert.Equal(1, battlefieldDestinationStep.ObjectChoiceCount);

        var promptCandidatesForFlow = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptCandidateDto>>(prompt.Candidates);
        Assert.Equal(promptCandidatesForFlow.Count, serverFlow.CandidateCount);
        Assert.Equal(promptCandidatesForFlow.Count(candidate => candidate.Enabled), serverFlow.EnabledCandidateCount);
        Assert.Equal(promptCandidatesForFlow.Count(candidate => !candidate.Enabled), serverFlow.DisabledCandidateCount);
        Assert.Contains(serverFlow.RelatedObjectIds, objectId => string.Equals(objectId, "P1-HAND-UNIT", StringComparison.Ordinal));
        Assert.Contains(serverFlow.RelatedObjectIds, objectId => string.Equals(objectId, "BF-1", StringComparison.Ordinal));
        var sourceFlowRef = Assert.Single(
            serverFlow.RelatedObjects,
            relatedObject => string.Equals(relatedObject.ObjectId, "P1-HAND-UNIT", StringComparison.Ordinal)
                && string.Equals(relatedObject.Role, "候选来源", StringComparison.Ordinal));
        Assert.Equal(["来源"], sourceFlowRef.CandidateRoles ?? []);
        Assert.Equal(1, sourceFlowRef.EnabledCandidateCount);
        Assert.Equal(0, sourceFlowRef.DisabledCandidateCount);
        Assert.Equal(ActionPromptContextSources.ServerActionPrompt, sourceFlowRef.CandidateSource);
        Assert.Contains("隐藏 metadata", sourceFlowRef.CandidateBoundary ?? string.Empty, StringComparison.Ordinal);
        var sourceFlowSourceStep = Assert.Single(
            sourceFlowRef.CandidateSteps ?? [],
            step => string.Equals(step.Role, "source", StringComparison.Ordinal));
        Assert.True(sourceFlowSourceStep.Required);
        Assert.Equal(1, sourceFlowSourceStep.ObjectChoiceCount);

        var battlefieldFlowRef = Assert.Single(
            serverFlow.RelatedObjects,
            relatedObject => string.Equals(relatedObject.ObjectId, "BF-1", StringComparison.Ordinal)
                && string.Equals(relatedObject.Role, "候选位置", StringComparison.Ordinal));
        Assert.Equal(["位置"], battlefieldFlowRef.CandidateRoles ?? []);
        Assert.Equal(1, battlefieldFlowRef.EnabledCandidateCount);
        var battlefieldFlowDestinationStep = Assert.Single(
            battlefieldFlowRef.CandidateSteps ?? [],
            step => string.Equals(step.Role, "destination", StringComparison.Ordinal));
        Assert.Equal(1, battlefieldFlowDestinationStep.ObjectChoiceCount);
    }

    [Fact]
    public void ServerFlowStepsKeepCandidateStepWhenRuleQueuesAreCrowded()
    {
        var state = CrowdedServerFlowState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        var serverFlow = Assert.IsType<ActionPromptServerFlowDto>(prompt.ServerFlow);
        var stepKeys = serverFlow.Steps.Select(step => step.Key).ToArray();

        Assert.Contains("prompt", stepKeys);
        Assert.Contains("responsibility", stepKeys);
        Assert.Contains("stack", stepKeys);
        Assert.Contains("task", stepKeys);
        Assert.Contains("trigger", stepKeys);
        Assert.Contains("candidate", stepKeys);
        Assert.True(serverFlow.Steps.Count >= 6);
        var candidateStep = Assert.Single(serverFlow.Steps, step => string.Equals(step.Key, "candidate", StringComparison.Ordinal));
        Assert.Equal("候选", candidateStep.Label);
        Assert.NotNull(prompt.Candidates);
        var promptCandidates = prompt.Candidates!;
        Assert.Equal(promptCandidates.Count, serverFlow.CandidateCount);
        Assert.Equal(promptCandidates.Count(candidate => candidate.Enabled), serverFlow.EnabledCandidateCount);
        Assert.Equal(promptCandidates.Count(candidate => !candidate.Enabled), serverFlow.DisabledCandidateCount);
        Assert.NotEmpty(promptCandidates);
        var expectedCandidateValue = string.Join(
            " / ",
            promptCandidates
                .Select(candidate => candidate.Action)
                .Distinct(StringComparer.Ordinal));
        Assert.Equal(expectedCandidateValue, candidateStep.Value);
        Assert.Contains("WAIT", candidateStep.Value, StringComparison.Ordinal);
        Assert.Contains(CommandTypes.Surrender, candidateStep.Value, StringComparison.Ordinal);
        Assert.Contains("前端只提交服务端候选", candidateStep.Detail, StringComparison.Ordinal);
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
    public void DeclareBattlePromptSelectionStepsReflectRequiredCommandTemplateRoles()
    {
        var state = BattleStateForConquest();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        var declareBattleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.DeclareBattle, StringComparison.Ordinal));

        var template = Assert.IsType<ActionPromptCommandTemplateDto>(declareBattleCandidate.CommandTemplate);
        Assert.Equal(CommandTypes.DeclareBattle, template.CmdType);
        Assert.Contains(template.Bindings, binding =>
            string.Equals(binding.Source, "selectedSource", StringComparison.Ordinal)
            && binding.Required);
        Assert.Contains(template.Bindings, binding =>
            string.Equals(binding.Source, "selectedDestination", StringComparison.Ordinal)
            && binding.Required);
        Assert.Contains(template.Bindings, binding =>
            string.Equals(binding.Source, "selectedTargets", StringComparison.Ordinal)
            && binding.Required);

        var steps = declareBattleCandidate.SelectionSteps ?? [];
        Assert.Contains(steps, step =>
            string.Equals(step.Role, "source", StringComparison.Ordinal)
            && step.Required
            && step.Choices.Count > 0);
        Assert.Contains(steps, step =>
            string.Equals(step.Role, "target", StringComparison.Ordinal)
            && step.Required
            && step.Choices.Count > 0);
        Assert.Contains(steps, step =>
            string.Equals(step.Role, "destination", StringComparison.Ordinal)
            && step.Required
            && step.Choices.Count > 0);
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

    [Fact]
    public async Task LocalTwoPlayerFlowTapsRecyclesPlaysResolvesScoresAdvancesTurnAndKeepsHiddenInfoSafe()
    {
        var engine = new CoreRuleEngine();
        var state = LocalTwoPlayerIntegratedFlowState();

        var tapped = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-local-2p-tap-rune", "P1", CommandTypes.TapRune),
            new TapRuneCommand("P1-RUNE-MANA"),
            CancellationToken.None);

        Assert.True(tapped.Accepted, tapped.ErrorMessage);
        Assert.True(tapped.State.CardObjects["P1-RUNE-MANA"].IsExhausted);
        Assert.Equal(4, tapped.State.RunePools["P1"].Mana);
        Assert.Contains(tapped.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_TAPPED", StringComparison.Ordinal));
        Assert.Contains(tapped.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));

        var recycled = await engine.ResolveAsync(
            tapped.State,
            new PlayerIntent("intent-local-2p-recycle-rune", "P1", CommandTypes.RecycleRune),
            new RecycleRuneCommand("P1-RUNE-POWER"),
            CancellationToken.None);

        Assert.True(recycled.Accepted, recycled.ErrorMessage);
        Assert.Equal(["P1-RUNE-MANA"], recycled.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM", "P1-RUNE-POWER"], recycled.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(1, recycled.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(new ObjectLocationState("P1", "RUNE_DECK"), recycled.State.ObjectLocations["P1-RUNE-POWER"]);
        Assert.Contains(recycled.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.Contains(recycled.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));

        var played = await engine.ResolveAsync(
            recycled.State,
            new PlayerIntent("intent-local-2p-play-unit-to-controlled-empty-battlefield", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-HAND-UNIT",
                "SFD·125/221",
                [],
                Destination: "BATTLEFIELD:BF-1"),
            CancellationToken.None);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(TimingStates.NeutralClosed, played.State.TimingState);
        Assert.Equal(new ObjectLocationState("P1", "STACK"), played.State.ObjectLocations["P1-HAND-UNIT"]);
        Assert.Contains(played.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal));
        Assert.Contains(played.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(played.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var p1PriorityPass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-local-2p-p1-pass-priority", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1PriorityPass.Accepted, p1PriorityPass.ErrorMessage);

        var p2PriorityPass = await engine.ResolveAsync(
            p1PriorityPass.State,
            new PlayerIntent("intent-local-2p-p2-pass-priority", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2PriorityPass.Accepted, p2PriorityPass.ErrorMessage);
        Assert.Equal(TimingStates.SpellDuelOpen, p2PriorityPass.State.TimingState);
        Assert.Equal("P1", p2PriorityPass.State.FocusPlayerId);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF-1"), p2PriorityPass.State.ObjectLocations["P1-HAND-UNIT"]);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL"],
            p2PriorityPass.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());

        var p1FocusPass = await engine.ResolveAsync(
            p2PriorityPass.State,
            new PlayerIntent("intent-local-2p-p1-pass-focus", "P1", CommandTypes.PassFocus),
            new PassFocusCommand(),
            CancellationToken.None);
        Assert.True(p1FocusPass.Accepted, p1FocusPass.ErrorMessage);

        var p2FocusPass = await engine.ResolveAsync(
            p1FocusPass.State,
            new PlayerIntent("intent-local-2p-p2-pass-focus", "P2", CommandTypes.PassFocus),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.True(p2FocusPass.Accepted, p2FocusPass.ErrorMessage);
        Assert.Equal(TimingStates.NeutralOpen, p2FocusPass.State.TimingState);
        Assert.Equal("P1", p2FocusPass.State.CardObjects["BF-1"].ControllerId);
        Assert.Equal(1, p2FocusPass.State.PlayerScores["P1"]);
        Assert.Contains(p2FocusPass.Events, gameEvent => string.Equals(gameEvent.Kind, "SPELL_DUEL_CLOSED", StringComparison.Ordinal));
        Assert.Contains(p2FocusPass.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(p2FocusPass.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.Contains(p2FocusPass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SCORE_GAINED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "BATTLEFIELD_CONQUERED_SCORE", StringComparison.Ordinal));
        AssertSnapshotDoesNotExposeObjectId(p2FocusPass.Snapshots["P1"], "P2-HIDDEN-HAND");
        AssertSnapshotDoesNotExposeObjectId(p2FocusPass.Snapshots["P1"], "P2-DRAW");
        AssertSnapshotDoesNotExposeObjectId(p2FocusPass.Snapshots["P1"], "P2-RUNE-DECK");
        AssertSnapshotDoesNotExposeObjectId(p2FocusPass.Snapshots["P2"], "P1-HIDDEN-HAND");
        AssertSnapshotDoesNotExposeObjectId(p2FocusPass.Snapshots["P2"], "P1-RUNE-BOTTOM");
        AssertLocalTwoPlayerTableAuthority(p2FocusPass, battlefieldScoredThisTurn: true);

        var p2CalledRuneObjectIds = p2FocusPass.State.PlayerZones["P2"].RuneDeck
            .Take(3)
            .ToArray();
        var p2DrawnObjectIds = p2FocusPass.State.PlayerZones["P2"].MainDeck
            .Take(1)
            .ToArray();

        var p1EndsTurn = await engine.ResolveAsync(
            p2FocusPass.State,
            new PlayerIntent("intent-local-2p-p1-end-turn", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(p1EndsTurn.Accepted, p1EndsTurn.ErrorMessage);
        Assert.Equal("P2", p1EndsTurn.State.ActivePlayerId);
        Assert.Equal("P2", p1EndsTurn.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, p1EndsTurn.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, p1EndsTurn.State.TimingState);
        AssertLocalTwoPlayerEndTurnAuthority(p1EndsTurn, p2CalledRuneObjectIds, p2DrawnObjectIds);
        AssertLocalTwoPlayerTableAuthority(p1EndsTurn, battlefieldScoredThisTurn: false);
        AssertSnapshotDoesNotExposeObjectId(p1EndsTurn.Snapshots["P1"], "P2-HIDDEN-HAND");
        AssertSnapshotDoesNotExposeObjectId(p1EndsTurn.Snapshots["P2"], "P1-HIDDEN-HAND");
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

    private static MatchState CrowdedServerFlowState()
    {
        return new MatchState(
            roomId: "local-playability-crowded-server-flow",
            tick: 11,
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
                    Hand = ["P1-HAND-UNIT"],
                    Battlefields = ["P1-FIELD-UNIT"]
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
                ["P1-FIELD-UNIT"] = Unit("P1-FIELD-UNIT", "P1", power: 2),
                ["P2-DEFENDER"] = Unit("P2-DEFENDER", "P2", power: 2)
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P2", "BATTLEFIELD", "BF-1"),
                ["P1-HAND-UNIT"] = new("P1", "HAND"),
                ["P1-FIELD-UNIT"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P2-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-1")
            },
            stackItems:
            [
                new StackItemState(
                    "stack-crowded-1",
                    "P1",
                    "P1-FIELD-UNIT",
                    "DAMAGE",
                    "SFD·125/221",
                    targetObjectIds: ["P2-DEFENDER"])
            ],
            triggerQueue:
            [
                new TriggerQueueItemState("trigger-crowded-1", "P1", "P1-FIELD-UNIT", "TEST_TRIGGER", "UNIT_ENTERED")
            ]);
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

    private static MatchState LocalTwoPlayerIntegratedFlowState()
    {
        return new MatchState(
            roomId: "local-playability-integrated-2p-flow",
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
                ["P1"] = new(3, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNIT", "P1-HIDDEN-HAND"],
                    Base = ["P1-RUNE-MANA", "P1-RUNE-POWER"],
                    RuneDeck = ["P1-RUNE-BOTTOM"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HIDDEN-HAND"],
                    MainDeck = ["P2-DRAW"],
                    RuneDeck = ["P2-RUNE-DECK"],
                    Battlefields = ["BF-1"]
                }
            },
            playerScores: Scores(),
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P2", "OGN·276/298"),
                ["P1-HAND-UNIT"] = Unit("P1-HAND-UNIT", "P1", power: 2),
                ["P1-HIDDEN-HAND"] = Unit("P1-HIDDEN-HAND", "P1", power: 2),
                ["P1-RUNE-MANA"] = Rune("P1-RUNE-MANA", "P1"),
                ["P1-RUNE-POWER"] = Rune("P1-RUNE-POWER", "P1"),
                ["P1-RUNE-BOTTOM"] = Rune("P1-RUNE-BOTTOM", "P1"),
                ["P2-HIDDEN-HAND"] = Unit("P2-HIDDEN-HAND", "P2", power: 2),
                ["P2-DRAW"] = Unit("P2-DRAW", "P2", power: 2),
                ["P2-RUNE-DECK"] = Rune("P2-RUNE-DECK", "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P2", "BATTLEFIELD", "BF-1"),
                ["P1-HAND-UNIT"] = new("P1", "HAND"),
                ["P1-HIDDEN-HAND"] = new("P1", "HAND"),
                ["P1-RUNE-MANA"] = new("P1", "BASE"),
                ["P1-RUNE-POWER"] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM"] = new("P1", "RUNE_DECK"),
                ["P2-HIDDEN-HAND"] = new("P2", "HAND"),
                ["P2-DRAW"] = new("P2", "MAIN_DECK"),
                ["P2-RUNE-DECK"] = new("P2", "RUNE_DECK")
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
            tags: [CardObjectTags.RuneCard, $"COLOR:{RuneTrait.Red}"],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static void AssertSnapshotDoesNotExposeObjectId(SnapshotDto snapshot, string hiddenObjectId)
    {
        var serializedSnapshot = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain(hiddenObjectId, serializedSnapshot, StringComparison.Ordinal);
    }

    private static void AssertLocalTwoPlayerEndTurnAuthority(
        ResolutionResult result,
        IReadOnlyList<string> p2CalledRuneObjectIds,
        IReadOnlyList<string> p2DrawnObjectIds)
    {
        Assert.Equal(
            [
                "TURN_END_DECLARED",
                "TURN_END_CLEANUP_STARTED",
                "UNTIL_END_OF_TURN_EXPIRED",
                "RUNE_POOL_CLEARED",
                "CLEANUP_REPEATED",
                "TURN_PLAYER_ADVANCED",
                "TURN_START_BEGAN",
                "RUNES_CALLED",
                "CARD_DRAWN",
                "RUNE_POOL_CLEARED",
                "MAIN_PHASE_BEGAN"
            ],
            result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(["P2-RUNE-DECK"], p2CalledRuneObjectIds);
        Assert.Equal(["P2-DRAW"], p2DrawnObjectIds);
        Assert.Equal(p2CalledRuneObjectIds, result.State.PlayerZones["P2"].Base);
        Assert.Equal(["P2-HIDDEN-HAND", "P2-DRAW"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.PlayerZones["P2"].MainDeck);
        Assert.Empty(result.State.PlayerZones["P2"].RuneDeck);
        Assert.Equal(new ObjectLocationState("P2", "BASE"), result.State.ObjectLocations["P2-RUNE-DECK"]);
        Assert.Equal(new ObjectLocationState("P2", "HAND"), result.State.ObjectLocations["P2-DRAW"]);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P2"]);
    }

    private static void AssertLocalTwoPlayerTableAuthority(ResolutionResult result, bool battlefieldScoredThisTurn)
    {
        var p1Snapshot = result.Snapshots["P1"];
        var p2Snapshot = result.Snapshots["P2"];
        Assert.Equal(1, Assert.IsType<int>(PlayerView(p1Snapshot, "P1")["score"]));
        Assert.Equal(1, Assert.IsType<int>(PlayerView(p2Snapshot, "P1")["score"]));

        var p1Table = Assert.IsType<SnapshotTableDto>(p1Snapshot.Table);
        var p2Table = Assert.IsType<SnapshotTableDto>(p2Snapshot.Table);
        var p1TableSelf = p1Table.Players.Single(player => string.Equals(player.PlayerId, "P1", StringComparison.Ordinal));
        var p1TableOpponent = p1Table.Players.Single(player => string.Equals(player.PlayerId, "P2", StringComparison.Ordinal));
        Assert.True(p1TableSelf.IsViewer);
        Assert.Equal("self", p1TableSelf.Perspective);
        Assert.Equal("opponent", p1TableOpponent.Perspective);
        Assert.Empty(p1TableOpponent.Zones.Hand);
        Assert.True(p1TableOpponent.Zones.HandHidden >= 1);

        var p2TableSelf = p2Table.Players.Single(player => string.Equals(player.PlayerId, "P2", StringComparison.Ordinal));
        var p2TableOpponent = p2Table.Players.Single(player => string.Equals(player.PlayerId, "P1", StringComparison.Ordinal));
        Assert.True(p2TableSelf.IsViewer);
        Assert.Equal("self", p2TableSelf.Perspective);
        Assert.Equal("opponent", p2TableOpponent.Perspective);
        Assert.Empty(p2TableOpponent.Zones.Hand);
        Assert.True(p2TableOpponent.Zones.HandHidden >= 1);

        var battlefield = p1Table.Battlefields.Single(field => string.Equals(field.BattlefieldObjectId, "BF-1", StringComparison.Ordinal));
        Assert.Equal("P1", battlefield.ControllerId);
        Assert.Equal(battlefieldScoredThisTurn, battlefield.ScoredThisTurn);
        Assert.Equal(battlefieldScoredThisTurn ? ["P1"] : [], battlefield.ScoredThisTurnPlayerIds);

        var rawBattlefield = BattlefieldView(p1Snapshot, "BF-1");
        Assert.Equal(battlefieldScoredThisTurn, Assert.IsType<bool>(rawBattlefield["scoredThisTurn"]));
        Assert.Equal(battlefieldScoredThisTurn ? ["P1"] : [], StringList(rawBattlefield["scoredThisTurnPlayerIds"]));
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

    private static Dictionary<string, object?> BattlefieldView(SnapshotDto snapshot, string battlefieldObjectId)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Lanes["battlefields"])
            .Single(item => string.Equals(item["battlefieldObjectId"] as string, battlefieldObjectId, StringComparison.Ordinal));
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
