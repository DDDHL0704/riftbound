using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StandbyDefendTeemoTests
{
    private const string BattlefieldObjectId = "P1-TEEMO-DEFEND-BATTLEFIELD";
    private const string AttackerObjectId = "P1-TEEMO-DEFEND-ATTACKER";
    private const string SecondEnemyTargetObjectId = "P1-TEEMO-DEFEND-SECOND-ENEMY";
    private const string TeemoObjectId = "P2-TEEMO-FACE-UP-DEFENDER";
    private const string SecondDefenderObjectId = "P2-TEEMO-FACE-UP-SECOND-DEFENDER";
    private const string ExhaustedFriendlyUnitObjectId = "P2-TEEMO-FACE-UP-EXHAUSTED-FRIENDLY";

    public static TheoryData<string> TeemoCards()
    {
        return new TheoryData<string>
        {
            "OGN·121/298",
            "OGN·121a/298",
            "SFD·230/221",
            "SFD·230*/221"
        };
    }

    [Fact]
    public void DeclareBattlePromptMetadataExposesTeemoDefendTriggerTargetChoices()
    {
        var session = new MatchSession(
            BuildState("OGN·121/298", includeSecondEnemyTarget: true),
            new CoreRuleEngine(),
            NoopMatchJournal.Instance);

        var prompt = session.PromptFor("P1");

        Assert.Contains(CommandTypes.DeclareBattle, prompt.Actions);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(candidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            requirement => string.Equals(requirement["sourceObjectId"] as string, AttackerObjectId, StringComparison.Ordinal));

        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["minBattlefieldTargetCount"]));
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["maxBattlefieldTargetCount"]));
        Assert.Equal("", Assert.IsType<string>(sourceRequirement["battlefieldTargetScope"]));
        var battlefieldTargetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["battlefieldTargetChoicesByIndex"]);
        Assert.Empty(battlefieldTargetChoicesByIndex);
    }

    [Fact]
    public async Task DeclareBattlePromptMetadataMultiplexesBattlefieldSteadfastAndTeemoDefendTriggerTargets()
    {
        var initial = BuildState("OGN·121/298", includeSecondEnemyTarget: true, includeSecondDefender: true,
            battlefieldCardNo: "OGN·279/298", battlefieldControllerId: "P2");
        var session = new MatchSession(initial, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var prompt = session.PromptFor("P1");
        var candidate = Assert.Single(prompt.Candidates ?? [], candidate => candidate.Action == CommandTypes.DeclareBattle);
        var metadata = Assert.IsType<Dictionary<string, object?>>(candidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            requirement => Equals(requirement["sourceObjectId"], AttackerObjectId));
        // CN 383.4f / 466.2c: this choice belongs to the earlier defended skill.
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["minBattlefieldTargetCount"]));
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["maxBattlefieldTargetCount"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["battlefieldTargetChoicesByIndex"]));
        var result = await OpenFortifiedDefend();
        var choice = Assert.IsType<PendingCardChoiceState>(result.State.PendingCardChoice);
        Assert.Equal(BattlefieldObjectId, choice.SourceObjectId);
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Equal(new[] { AttackerObjectId, SecondEnemyTargetObjectId, TeemoObjectId, SecondDefenderObjectId }
            .Order(StringComparer.Ordinal), choice.LegalObjectIds.Order(StringComparer.Ordinal));
        Assert.Equal(TopFiveMainDeckCards().Length + 1, result.State.PlayerZones["P2"].MainDeck.Count);
        Assert.DoesNotContain(result.Events, e => e.Kind is "DAMAGE_APPLIED" or "MAIN_DECK_CARDS_REVEALED");
    }

    [Fact]
    public void DeclareBattlePromptMetadataMultiplexesBattlefieldMoveToBaseAndTeemoDefendTriggerTargets()
    {
        var session = new MatchSession(
            BuildState(
                "OGN·121/298",
                includeSecondEnemyTarget: true,
                includeSecondDefender: true,
                battlefieldCardNo: "OGN·285/298",
                battlefieldControllerId: "P2"),
            new CoreRuleEngine(),
            NoopMatchJournal.Instance);

        var prompt = session.PromptFor("P1");

        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(candidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            requirement => string.Equals(requirement["sourceObjectId"] as string, AttackerObjectId, StringComparison.Ordinal));

        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["minBattlefieldTargetCount"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["maxBattlefieldTargetCount"]));
        var battlefieldTargetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["battlefieldTargetChoicesByIndex"]);
        Assert.Equal(
            [TeemoObjectId, SecondDefenderObjectId],
            battlefieldTargetChoicesByIndex["0"].Select(choice => choice.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray());
        Assert.Single(battlefieldTargetChoicesByIndex);
    }

    [Fact]
    public void DeclareBattlePromptMetadataUsesPlunderFriendlyUnitScopeBeyondDefenders()
    {
        var session = new MatchSession(
            BuildState(
                "OGN·121/298",
                includeSecondEnemyTarget: true,
                battlefieldCardNo: "OGN·285/298",
                battlefieldControllerId: "P2",
                includeExhaustedFriendlyUnit: true),
            new CoreRuleEngine(),
            NoopMatchJournal.Instance);

        var prompt = session.PromptFor("P1");

        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(candidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            requirement => string.Equals(requirement["sourceObjectId"] as string, AttackerObjectId, StringComparison.Ordinal));

        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["minBattlefieldTargetCount"]));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["maxBattlefieldTargetCount"]));
        var battlefieldTargetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["battlefieldTargetChoicesByIndex"]);
        Assert.Equal(
            [TeemoObjectId, ExhaustedFriendlyUnitObjectId],
            battlefieldTargetChoicesByIndex["0"].Select(choice => choice.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray());
        Assert.Single(battlefieldTargetChoicesByIndex);
    }

    [Theory]
    [MemberData(nameof(TeemoCards))]
    public async Task FaceUpTeemoDefendTriggerCountsTopFiveStandbyCardsAndRecycles(string teemoCardNo)
    {
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(
            BuildState(teemoCardNo),
            new PlayerIntent("intent-teemo-face-up-defend-trigger", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [TeemoObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await ConfirmAndResolveDefend(result, AttackerObjectId);
        Assert.Equal("P2-MAIN-BOTTOM", result.State.PlayerZones["P2"].MainDeck[0]);
        Assert.Equal(
            TopFiveMainDeckCards().OrderBy(cardId => cardId, StringComparer.Ordinal).ToArray(),
            result.State.PlayerZones["P2"].MainDeck.Skip(1).OrderBy(cardId => cardId, StringComparer.Ordinal).ToArray());
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MAIN_DECK_CARDS_REVEALED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["count"], 5)
            && Equals(gameEvent.Payload["damageAmount"], 2));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["targetObjectId"], AttackerObjectId)
            && Equals(gameEvent.Payload["damage"], 2));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["count"], 5));
    }

    [Theory]
    [MemberData(nameof(TeemoCards))]
    public async Task ActualMovementIntoTeemoBattleConfirmsIndependentDefendTargetsBeforeRevealingCards(string teemoCardNo)
    {
        var state = LocalDestructionRecallTests.At(BuildState(teemoCardNo, battlefieldControllerId: "P2"), AttackerObjectId, "P1", "BASE");
        var deck = state.PlayerZones["P2"].MainDeck.ToArray();
        var moved = await OfficialInsightAndSpellLockTests.Act(state, "P1",
            new MoveUnitCommand(AttackerObjectId, "BASE", "BATTLEFIELD:" + BattlefieldObjectId, []));
        Assert.True(moved.State.CardObjects[TeemoObjectId].IsDefending);
        Assert.Contains(moved.Events, e => e.Kind == "BATTLE_ROLES_ESTABLISHED");
        Assert.Equal(deck, moved.State.PlayerZones["P2"].MainDeck);
        Assert.DoesNotContain(moved.Events, e => e.Kind is "MAIN_DECK_CARDS_REVEALED" or "CARDS_RECYCLED" or "DAMAGE_APPLIED");
        var choice = Assert.IsType<PendingCardChoiceState>(moved.State.PendingCardChoice);
        Assert.Equal("P2", choice.PlayerId);
        Assert.Equal(1, choice.RequiredCount);
        Assert.Equal(new[] { AttackerObjectId }, choice.LegalObjectIds);
    }

    [Theory]
    [MemberData(nameof(TeemoCards))]
    public async Task DefendCountUsesPrintedKeywordsWithoutTrustingOrRequiringObjectTags(string teemoCardNo)
    {
        var state = BuildState(teemoCardNo);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["P2-MAIN-STANDBY-001"] = state.CardObjects["P2-MAIN-STANDBY-001"] with { Tags = [CardObjectTags.UnitCard] },
            ["P2-MAIN-STANDBY-002"] = state.CardObjects["P2-MAIN-STANDBY-002"] with { Tags = [CardObjectTags.UnitCard] },
            ["P2-MAIN-NON-STANDBY-001"] = state.CardObjects["P2-MAIN-NON-STANDBY-001"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Standby] }
        }};
        var result = await new CoreRuleEngine().ResolveAsync(state, new("printed-count", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(BattlefieldObjectId, [AttackerObjectId], [TeemoObjectId], OptionalCosts: ["COMBAT_ASSIGNMENT"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        result = await ConfirmAndResolveDefend(result, AttackerObjectId);
        var revealed = Assert.Single(result.Events, e => e.Kind == "MAIN_DECK_CARDS_REVEALED"
            && Equals(e.Payload["sourceObjectId"], TeemoObjectId));
        Assert.Equal(2, revealed.Payload["damageAmount"]);
        Assert.Equal(new[] { "P2-MAIN-STANDBY-001", "P2-MAIN-STANDBY-002" },
            Assert.IsAssignableFrom<IEnumerable<string>>(revealed.Payload["countedCardIds"]).Order());
        Assert.DoesNotContain("P2-MAIN-NON-STANDBY-001", Assert.IsAssignableFrom<IEnumerable<string>>(revealed.Payload["countedCardIds"]));
        Assert.Contains(result.Events, e => e.Kind == "CARDS_RECYCLED" && Equals(e.Payload["count"], 5));
    }

    [Fact]
    public async Task FaceUpTeemoDefendTriggerUsesExplicitBattlefieldTargetWhenMultipleTargetsAreLegal()
    {
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(
            BuildState("OGN·121/298", includeSecondEnemyTarget: true),
            new PlayerIntent("intent-teemo-face-up-defend-trigger-explicit-target", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [TeemoObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"],
                BattlefieldTargetObjectIds: []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await ConfirmAndResolveDefend(result, SecondEnemyTargetObjectId);
        Assert.Equal("P2-MAIN-BOTTOM", result.State.PlayerZones["P2"].MainDeck[0]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MAIN_DECK_CARDS_REVEALED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["damageAmount"], 2));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["targetObjectId"], SecondEnemyTargetObjectId)
            && Equals(gameEvent.Payload["damage"], 2));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["count"], 5));
    }

    [Fact]
    public async Task FaceUpTeemoDefendTriggerMultiplexesBattlefieldSteadfastAndDamageTargets()
    {
        var result = await OpenFortifiedDefend();
        var events = result.Events.ToList();
        for (var confirmation = 0; result.State.PendingCardChoice is { } choice; confirmation++) {
            Assert.InRange(confirmation, 0, 1);
            var target = choice.SourceObjectId == BattlefieldObjectId ? TeemoObjectId : SecondEnemyTargetObjectId;
            Assert.Contains(target, choice.LegalObjectIds);
            result = await OfficialInsightAndSpellLockTests.Act(result.State, "P2",
                new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
            events.AddRange(result.Events);
            Assert.DoesNotContain(result.Events, e => e.Kind is "MAIN_DECK_CARDS_REVEALED" or "DAMAGE_APPLIED");
        }
        for (var skill = 0; result.State.StackItems.Count > 0; skill++) {
            Assert.InRange(skill, 0, 1);
            result = await OfficialGraveyardRecastTests.Top(result.State); events.AddRange(result.Events);
        }
        for (var pass = 0; result.State.FocusPlayerId is not null; pass++) {
            Assert.InRange(pass, 0, 3);
            result = await OfficialInsightAndSpellLockTests.Act(result.State, result.State.FocusPlayerId, new PassFocusCommand());
            events.AddRange(result.Events);
        }
        result = await OfficialInsightAndSpellLockTests.Act(result.State, "P1", new DeclareBattleCommand(
            BattlefieldObjectId, [AttackerObjectId, SecondEnemyTargetObjectId], [TeemoObjectId, SecondDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
        events.AddRange(result.Events);
        result = await CombatTestDriver.FinishAsync(result with { Events = events });
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["trigger"], TriggerKinds.BattlefieldDefendGrantSteadfast)
            && Equals(gameEvent.Payload["targetObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["keyword"], CardCombatKeywordNames.Steadfast)
            && Equals(gameEvent.Payload["keywordBonus"], 2));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["targetObjectId"], SecondEnemyTargetObjectId)
            && Equals(gameEvent.Payload["damage"], 2));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && PayloadEquals(gameEvent, "combatRole", "DEFENDER")
            && PayloadEquals(gameEvent, "sourceObjectId", TeemoObjectId)
            && PayloadEquals(gameEvent, "keyword", CardCombatKeywordNames.Steadfast)
            && PayloadEquals(gameEvent, "keywordBonus", 2));
    }

    [Fact]
    public async Task FaceUpTeemoDefendTriggerMultiplexesBattlefieldMoveToBaseAndDamageTargets()
    {
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(
            BuildState(
                "OGN·121/298",
                includeSecondEnemyTarget: true,
                includeSecondDefender: true,
                battlefieldCardNo: "OGN·285/298",
                battlefieldControllerId: "P2",
                attackerPower: 1),
            new PlayerIntent("intent-teemo-face-up-defend-trigger-multiplexed-move-to-base", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [TeemoObjectId, SecondDefenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"],
                BattlefieldTargetObjectIds: [TeemoObjectId]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await ConfirmAndResolveDefend(result, SecondEnemyTargetObjectId);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["trigger"], TriggerKinds.BattlefieldDefendMoveFriendlyUnitToBase)
            && Equals(gameEvent.Payload["targetObjectId"], TeemoObjectId));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["targetObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["destinationZone"], "BASE"));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["targetObjectId"], SecondEnemyTargetObjectId)
            && Equals(gameEvent.Payload["damage"], 2));
        Assert.Contains(TeemoObjectId, result.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain(TeemoObjectId, result.State.PlayerZones["P2"].Battlefields);
    }

    [Fact]
    public async Task FaceUpTeemoDefendTriggerLetsPlunderMoveAnotherDefenderBeforeAllAttackersDealDamage()
    {
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(
            BuildState(
                "OGN·121/298",
                includeSecondEnemyTarget: true,
                battlefieldCardNo: "OGN·285/298",
                battlefieldControllerId: "P2",
                includeExhaustedFriendlyUnit: true,
                attackerPower: 1),
            new PlayerIntent("intent-teemo-face-up-defend-trigger-multiplexed-plunder-friendly-unit", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [TeemoObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"],
                BattlefieldTargetObjectIds: [ExhaustedFriendlyUnitObjectId]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        result = await ConfirmAndResolveDefend(result, SecondEnemyTargetObjectId);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["trigger"], TriggerKinds.BattlefieldDefendMoveFriendlyUnitToBase)
            && Equals(gameEvent.Payload["targetObjectId"], ExhaustedFriendlyUnitObjectId));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["targetObjectId"], ExhaustedFriendlyUnitObjectId)
            && Equals(gameEvent.Payload["destinationZone"], "BASE"));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && Equals(gameEvent.Payload["sourceObjectId"], TeemoObjectId)
            && Equals(gameEvent.Payload["targetObjectId"], SecondEnemyTargetObjectId)
            && Equals(gameEvent.Payload["damage"], 2));
        Assert.Contains(ExhaustedFriendlyUnitObjectId, result.State.PlayerZones["P2"].Base);
        // Both enemy units at this battlefield attack; Teemo remains after the
        // movement trigger, then receives lethal damage during combat.
        Assert.Contains(TeemoObjectId, result.State.PlayerZones["P2"].Graveyard);
        Assert.Contains(result.Events, gameEvent => gameEvent.Kind == "UNIT_DESTROYED"
            && Equals(gameEvent.Payload["targetObjectId"], TeemoObjectId));
    }

    [Fact]
    public void DeclareBattlePromptBattlefieldDefendEffectTargetsUseGenericTriggerSpecs()
    {
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var matchSessionSource = File.ReadAllText(matchSessionPath);
        var promptHelper = matchSessionSource[
            matchSessionSource.IndexOf("private static IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> DeclareBattleBattlefieldDefendEffectTargetChoicesByIndex", StringComparison.Ordinal)..];
        promptHelper = promptHelper[..promptHelper.IndexOf("private static IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> DeclareBattleDefendTriggerTargetChoicesByIndex", StringComparison.Ordinal)];

        Assert.DoesNotContain("TryGetDeclareBattlePromptDefendedUnitTargetBattlefieldTrigger", promptHelper, StringComparison.Ordinal);
        Assert.DoesNotContain("BattlefieldTriggerSpecRules.TriggersForCard", promptHelper, StringComparison.Ordinal);
        Assert.Contains("BattlefieldTriggerSpecRules.TryGetTrigger", promptHelper, StringComparison.Ordinal);
        Assert.Contains("BattlefieldTriggerSpecRules.IsDeclareBattlePromptDefenderUnitTargetBattlefieldTrigger", promptHelper, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetBattlefieldDefendGrantSteadfastTrigger", promptHelper, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetBattlefieldDefendMoveFriendlyUnitToBaseTrigger", promptHelper, StringComparison.Ordinal);
    }

    private static async Task<ResolutionResult> OpenFortifiedDefend()
    {
        var state = BuildState("OGN·121/298", includeSecondEnemyTarget: true, includeSecondDefender: true,
            battlefieldCardNo: "OGN·279/298", battlefieldControllerId: "P2");
        foreach (var id in new[] { AttackerObjectId, SecondEnemyTargetObjectId })
            state = LocalDestructionRecallTests.At(state, id, "P1", "BASE");
        var result = await OfficialInsightAndSpellLockTests.Act(state, "P1", new MoveUnitCommand(
            AttackerObjectId, "BASE", "BATTLEFIELD:" + BattlefieldObjectId, [], [AttackerObjectId, SecondEnemyTargetObjectId]));
        Assert.Equal(2, result.State.TriggerQueue.Count);
        var ordering = result.Prompts.Single(p => p.Value.Actionable && p.Value.Actions.Contains(CommandTypes.OrderTriggers)).Key;
        var order = result.State.TriggerQueue.OrderBy(t => t.SourceObjectId == BattlefieldObjectId ? 0 : 1).Select(t => t.TriggerId).ToArray();
        var ordered = await OfficialInsightAndSpellLockTests.Act(result.State, ordering, new OrderTriggersCommand(OrderedTriggerIds: order));
        return ordered with { Events = result.Events.Concat(ordered.Events).ToArray() };
    }

    internal static async Task<ResolutionResult> ConfirmAndResolveDefend(ResolutionResult result, string target)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(result.State.PendingCardChoice);
        Assert.Equal("P2", choice.PlayerId);
        Assert.Equal(1, choice.RequiredCount);
        Assert.Contains(target, choice.LegalObjectIds);
        Assert.DoesNotContain(result.Events, e => e.Kind is "MAIN_DECK_CARDS_REVEALED" or "CARDS_RECYCLED" or "DAMAGE_APPLIED");
        OfficialGraveyardRecastTests.Restore(result.State);
        ArmamentMightTests.PublicRecovery(result.State);
        var selected = await OfficialInsightAndSpellLockTests.Act(result.State, "P2",
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        Assert.DoesNotContain(selected.Events, e => e.Kind is "MAIN_DECK_CARDS_REVEALED" or "CARDS_RECYCLED" or "DAMAGE_APPLIED");
        OfficialGraveyardRecastTests.Restore(selected.State);
        var done = await OfficialGraveyardRecastTests.Top(selected.State);
        var events = result.Events.Concat(selected.Events).Concat(done.Events).ToList();
        // The independent ability closes first; then both players pass the retained battle response window.
        for (var pass = 0; pass < 2 && done.State.StackItems.Count == 0
            && done.State.TimingState == TimingStates.NeutralClosed && done.State.PriorityPlayerId is not null; pass++)
        {
            done = await OfficialInsightAndSpellLockTests.Act(done.State, done.State.PriorityPlayerId, new PassPriorityCommand());
            events.AddRange(done.Events);
        }
        return done with { Events = events };
    }

    private static bool PayloadEquals(GameEvent gameEvent, string key, object? expected)
    {
        return gameEvent.Payload.TryGetValue(key, out var actual)
            && Equals(actual, expected);
    }

    internal static MatchState BuildState(
        string teemoCardNo,
        bool includeSecondEnemyTarget = false,
        bool includeSecondDefender = false,
        string battlefieldCardNo = "OGN·278/298",
        string battlefieldControllerId = "P1",
        int attackerPower = 7,
        bool includeExhaustedFriendlyUnit = false)
    {
        var topFive = TopFiveMainDeckCards();
        return new MatchState(
            "standby-defend-teemo-room",
            51,
            7,
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
                    Battlefields = P1BattlefieldObjects(battlefieldControllerId, includeSecondEnemyTarget)
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = P2BattlefieldObjects(
                        battlefieldControllerId,
                        includeSecondDefender,
                        includeExhaustedFriendlyUnit),
                    MainDeck = [.. topFive, "P2-MAIN-BOTTOM"]
                }
            },
            cardObjects: BuildCardObjects(
                teemoCardNo,
                includeSecondEnemyTarget,
                includeSecondDefender,
                battlefieldCardNo,
                battlefieldControllerId,
                attackerPower,
                includeExhaustedFriendlyUnit),
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted(BattlefieldObjectId)],
            objectLocations: BuildObjectLocations(
                includeSecondEnemyTarget,
                includeSecondDefender,
                battlefieldControllerId,
                includeExhaustedFriendlyUnit));
    }

    private static string[] P1BattlefieldObjects(string battlefieldControllerId, bool includeSecondEnemyTarget)
    {
        var objectIds = new List<string>();
        if (string.Equals(battlefieldControllerId, "P1", StringComparison.Ordinal))
        {
            objectIds.Add(BattlefieldObjectId);
        }

        objectIds.Add(AttackerObjectId);
        if (includeSecondEnemyTarget)
        {
            objectIds.Add(SecondEnemyTargetObjectId);
        }

        return objectIds.ToArray();
    }

    private static string[] P2BattlefieldObjects(
        string battlefieldControllerId,
        bool includeSecondDefender,
        bool includeExhaustedFriendlyUnit)
    {
        var objectIds = new List<string>();
        if (string.Equals(battlefieldControllerId, "P2", StringComparison.Ordinal))
        {
            objectIds.Add(BattlefieldObjectId);
        }

        objectIds.Add(TeemoObjectId);
        if (includeSecondDefender)
        {
            objectIds.Add(SecondDefenderObjectId);
        }
        if (includeExhaustedFriendlyUnit)
        {
            objectIds.Add(ExhaustedFriendlyUnitObjectId);
        }

        return objectIds.ToArray();
    }

    private static Dictionary<string, CardObjectState> BuildCardObjects(
        string teemoCardNo,
        bool includeSecondEnemyTarget,
        bool includeSecondDefender,
        string battlefieldCardNo,
        string battlefieldControllerId,
        int attackerPower,
        bool includeExhaustedFriendlyUnit)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new(
                BattlefieldObjectId,
                cardNo: battlefieldCardNo,
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: battlefieldControllerId,
                controllerId: battlefieldControllerId),
            [AttackerObjectId] = Unit(AttackerObjectId, "P1", "UNL-057/219", attackerPower),
            [TeemoObjectId] = Unit(TeemoObjectId, "P2", teemoCardNo, 2, ["约德尔人"]),
            ["P2-MAIN-STANDBY-001"] = Unit("P2-MAIN-STANDBY-001", "P2", "OGN·121/298", 2, [CardObjectTags.Standby]),
            ["P2-MAIN-STANDBY-002"] = Unit("P2-MAIN-STANDBY-002", "P2", "OGN·199/298", 2, [CardObjectTags.Standby]),
            ["P2-MAIN-NON-STANDBY-001"] = Unit("P2-MAIN-NON-STANDBY-001", "P2", "SFD·125/221", 3),
            ["P2-MAIN-NON-STANDBY-002"] = new(
                "P2-MAIN-NON-STANDBY-002",
                cardNo: "OGN·009/298",
                tags: [CardObjectTags.SpellCard],
                ownerId: "P2",
                controllerId: "P2"),
            ["P2-MAIN-NON-STANDBY-003"] = Unit("P2-MAIN-NON-STANDBY-003", "P2", "SFD·125/221", 3),
            ["P2-MAIN-BOTTOM"] = Unit("P2-MAIN-BOTTOM", "P2", "SFD·125/221", 3)
        };
        if (includeSecondEnemyTarget)
        {
            cardObjects[SecondEnemyTargetObjectId] = Unit(SecondEnemyTargetObjectId, "P1", "UNL-092/219", 3);
        }
        if (includeSecondDefender)
        {
            cardObjects[SecondDefenderObjectId] = Unit(SecondDefenderObjectId, "P2", "SFD·125/221", 3, [CardCombatKeywordNames.Bulwark]);
        }
        if (includeExhaustedFriendlyUnit)
        {
            cardObjects[ExhaustedFriendlyUnitObjectId] = Unit(
                ExhaustedFriendlyUnitObjectId,
                "P2",
                "SFD·125/221",
                3,
                isExhausted: true);
        }

        return cardObjects;
    }

    private static Dictionary<string, ObjectLocationState> BuildObjectLocations(
        bool includeSecondEnemyTarget,
        bool includeSecondDefender,
        string battlefieldControllerId,
        bool includeExhaustedFriendlyUnit)
    {
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new(battlefieldControllerId, "BATTLEFIELD", BattlefieldObjectId),
            [AttackerObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId),
            [TeemoObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
        };
        if (includeSecondEnemyTarget)
        {
            objectLocations[SecondEnemyTargetObjectId] = new("P1", "BATTLEFIELD", BattlefieldObjectId);
        }
        if (includeSecondDefender)
        {
            objectLocations[SecondDefenderObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId);
        }
        if (includeExhaustedFriendlyUnit)
        {
            objectLocations[ExhaustedFriendlyUnitObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId);
        }

        return objectLocations;
    }

    private static string[] TopFiveMainDeckCards()
    {
        return
        [
            "P2-MAIN-STANDBY-001",
            "P2-MAIN-NON-STANDBY-001",
            "P2-MAIN-STANDBY-002",
            "P2-MAIN-NON-STANDBY-002",
            "P2-MAIN-NON-STANDBY-003"
        ];
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        string cardNo,
        int power,
        IReadOnlyList<string>? extraTags = null,
        bool isExhausted = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            tags: [.. new[] { CardObjectTags.UnitCard }.Concat(extraTags ?? [])],
            ownerId: playerId,
            controllerId: playerId,
            isExhausted: isExhausted);
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "riftbound-dotnet.sln"))
                || File.Exists(Path.Combine(current.FullName, "Riftbound.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test output directory.");
    }
}
