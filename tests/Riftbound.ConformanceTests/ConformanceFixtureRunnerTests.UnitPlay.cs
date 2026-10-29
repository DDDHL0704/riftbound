using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public void GemstoneSeerPredictPromptExposesOnlyFriendlyTopMainDeckCard()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-OGN-GEMSTONE-SEER"],
                    MainDeck = ["P1-GEMSTONE-SEER-TOP-001", "P1-GEMSTONE-SEER-SECOND-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-GEMSTONE-SEER-HIDDEN-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-OGN-GEMSTONE-SEER"] = new(
                    "P1-UNIT-OGN-GEMSTONE-SEER",
                    cardNo: "OGN·100/298",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardLifecycleKeywordNames.Predict],
                    manaCost: 3,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GEMSTONE-SEER-TOP-001"] = new(
                    "P1-GEMSTONE-SEER-TOP-001",
                    cardNo: "UNL-007/219",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GEMSTONE-SEER-SECOND-001"] = new(
                    "P1-GEMSTONE-SEER-SECOND-001",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-GEMSTONE-SEER-HIDDEN-001"] = new(
                    "P2-GEMSTONE-SEER-HIDDEN-001",
                    cardNo: "UNL-007/219",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-UNIT-OGN-GEMSTONE-SEER", StringComparison.Ordinal));

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(
                requirement["sourceObjectId"] as string,
                "P1-UNIT-OGN-GEMSTONE-SEER",
                StringComparison.Ordinal));

        Assert.Equal(0,Assert.IsType<int>(sourceRequirement["minTargetCount"]));
        Assert.Equal(0,Assert.IsType<int>(sourceRequirement["maxTargetCount"]));
        var choicesByIndex=Assert.IsAssignableFrom<IReadOnlyDictionary<string,object?>>(sourceRequirement["targetChoicesByIndex"]);
        Assert.Empty(choicesByIndex);
    }

    [Fact]
    public void PrescientMechStaticGrantedPredictPromptExposesOnlyFriendlyTopMainDeckCardForMechanicalUnit()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-PROGRESS-GLORY"],
                    Base = ["P1-UNIT-PRESCIENT-MECH"],
                    MainDeck = ["P1-PROGRESS-GLORY-TOP-001", "P1-PROGRESS-GLORY-SECOND-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-PROGRESS-GLORY-HIDDEN-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-PRESCIENT-MECH"] = new(
                    "P1-UNIT-PRESCIENT-MECH",
                    cardNo: "SFD·065/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "机械", "约德尔人"],
                    manaCost: 2,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-PROGRESS-GLORY"] = new(
                    "P1-UNIT-PROGRESS-GLORY",
                    cardNo: "SFD·075/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "机械"],
                    manaCost: 4,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-PROGRESS-GLORY-TOP-001"] = new(
                    "P1-PROGRESS-GLORY-TOP-001",
                    cardNo: "UNL-007/219",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-PROGRESS-GLORY-SECOND-001"] = new(
                    "P1-PROGRESS-GLORY-SECOND-001",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-PROGRESS-GLORY-HIDDEN-001"] = new(
                    "P2-PROGRESS-GLORY-HIDDEN-001",
                    cardNo: "UNL-007/219",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-UNIT-PROGRESS-GLORY", StringComparison.Ordinal));

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(
                requirement["sourceObjectId"] as string,
                "P1-UNIT-PROGRESS-GLORY",
                StringComparison.Ordinal));

        Assert.Equal(0,Assert.IsType<int>(sourceRequirement["minTargetCount"]));
        Assert.Equal(0,Assert.IsType<int>(sourceRequirement["maxTargetCount"]));
        var choicesByIndex=Assert.IsAssignableFrom<IReadOnlyDictionary<string,object?>>(sourceRequirement["targetChoicesByIndex"]);
        Assert.Empty(choicesByIndex);
    }

    [Fact]
    public void GemstoneSeerStaticGrantedPredictPromptExposesOnlyFriendlyTopMainDeckCardForOtherFriendlyUnit()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-PROGRESS-GLORY"],
                    Base = ["P1-UNIT-OGN-GEMSTONE-SEER"],
                    MainDeck = ["P1-PROGRESS-GLORY-TOP-001", "P1-PROGRESS-GLORY-SECOND-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-PROGRESS-GLORY-HIDDEN-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-OGN-GEMSTONE-SEER"] = new(
                    "P1-UNIT-OGN-GEMSTONE-SEER",
                    cardNo: "OGN·100/298",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardLifecycleKeywordNames.Predict],
                    manaCost: 3,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-PROGRESS-GLORY"] = new(
                    "P1-UNIT-PROGRESS-GLORY",
                    cardNo: "SFD·075/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "机械"],
                    manaCost: 4,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-PROGRESS-GLORY-TOP-001"] = new(
                    "P1-PROGRESS-GLORY-TOP-001",
                    cardNo: "UNL-007/219",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-PROGRESS-GLORY-SECOND-001"] = new(
                    "P1-PROGRESS-GLORY-SECOND-001",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-PROGRESS-GLORY-HIDDEN-001"] = new(
                    "P2-PROGRESS-GLORY-HIDDEN-001",
                    cardNo: "UNL-007/219",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-UNIT-PROGRESS-GLORY", StringComparison.Ordinal));

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(
                requirement["sourceObjectId"] as string,
                "P1-UNIT-PROGRESS-GLORY",
                StringComparison.Ordinal));

        Assert.Equal(0,Assert.IsType<int>(sourceRequirement["minTargetCount"]));
        Assert.Equal(0,Assert.IsType<int>(sourceRequirement["maxTargetCount"]));
        var choicesByIndex=Assert.IsAssignableFrom<IReadOnlyDictionary<string,object?>>(sourceRequirement["targetChoicesByIndex"]);
        Assert.Empty(choicesByIndex);
    }

    [Theory]
    [InlineData("p2-preflight-play-unl-babbling-poro-predict-recycle.fixture.json", "P1-UNIT-UNL-BABBLING-PORO", "P1-UNL-BABBLING-PORO-KEEP-001", "P1-UNL-BABBLING-PORO-RECYCLE-001", 2, "CARD_TYPE:UNIT|预知|魄罗")]
    [InlineData("p2-preflight-play-babbling-poro-predict-recycle.fixture.json", "P1-UNIT-BABBLING-PORO", "P1-BABBLING-PORO-KEEP-001", "P1-BABBLING-PORO-RECYCLE-001", 2, "CARD_TYPE:UNIT|预知|魄罗")]
    [InlineData("p2-preflight-play-gemstone-golem-predict-recycle.fixture.json", "P1-UNIT-GEMSTONE-GOLEM", "P1-GEMSTONE-GOLEM-KEEP-001", "P1-GEMSTONE-GOLEM-RECYCLE-001", 5, "CARD_TYPE:UNIT|坚守|预知")]
    [InlineData("p2-preflight-play-ogn-gemstone-seer-predict-recycle.fixture.json", "P1-UNIT-OGN-GEMSTONE-SEER", "P1-GEMSTONE-SEER-KEEP-001", "P1-GEMSTONE-SEER-RECYCLE-001", 3, "CARD_TYPE:UNIT|预知")]
    [InlineData("p2-preflight-play-dase-scout-predict-recycle.fixture.json", "P1-UNIT-DASE-SCOUT", "P1-DASE-SCOUT-KEEP-001", "P1-DASE-SCOUT-RECYCLE-001", 5, "CARD_TYPE:UNIT|预知")]
    [InlineData("p2-preflight-play-jhin-predict-recycle.fixture.json", "P1-UNIT-JHIN", "P1-JHIN-KEEP-001", "P1-JHIN-RECYCLE-001", 4, "CARD_TYPE:UNIT|预知")]
    [InlineData("p2-preflight-play-jhin-alt-a-predict-recycle.fixture.json", "P1-UNIT-JHIN-A", "P1-JHIN-A-KEEP-001", "P1-JHIN-A-RECYCLE-001", 4, "CARD_TYPE:UNIT|预知")]
    public async Task CoreRuleEnginePlaysPredictSourceUnitRecycleTopCard(
        string fixtureFileName,
        string sourceObjectId,
        string expectedTopObjectId,
        string expectedRecycledObjectId,
        int expectedPower,
        string expectedTags)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal([sourceObjectId], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([expectedTopObjectId, expectedRecycledObjectId], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(expectedPower, result.FinalState.CardObjects[sourceObjectId].Power);
        Assert.Equal(expectedTags.Split('|'), result.FinalState.CardObjects[sourceObjectId].Tags);
        if (expectedTags.Split('|').Contains("急速", StringComparer.Ordinal))
        {
            Assert.True(result.FinalState.CardObjects[sourceObjectId].IsExhausted);
        }
    }

    [Fact]
    public async Task CoreRuleEnginePlaysStaticGrantedPredictSourceUnitRecycleTopCard()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "p2-preflight-play-progress-glory-static-granted-predict-recycle.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        var differences = ConformanceFixtureRunner.CompareExpected(fixture, result);
        Assert.True(differences.Count == 0, string.Join("; ", differences));
        Assert.Equal(
            ["P1-UNIT-PRESCIENT-MECH", "P1-UNIT-PROGRESS-GLORY"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P1-PROGRESS-GLORY-KEEP-001", "P1-PROGRESS-GLORY-RECYCLE-001"],
            result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-PROGRESS-GLORY"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, "机械"],
            result.FinalState.CardObjects["P1-UNIT-PROGRESS-GLORY"].Tags);
        Assert.Contains(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
                && string.Equals(
                    gameEvent.Payload["sourceObjectId"] as string,
                    "P1-UNIT-PROGRESS-GLORY",
                    StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysOtherFriendlyStaticGrantedPredictSourceUnitRecycleTopCard()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-PROGRESS-GLORY"],
                    Base = ["P1-UNIT-OGN-GEMSTONE-SEER"],
                    MainDeck = ["P1-PROGRESS-GLORY-RECYCLE-001", "P1-PROGRESS-GLORY-KEEP-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-OGN-GEMSTONE-SEER"] = new(
                    "P1-UNIT-OGN-GEMSTONE-SEER",
                    cardNo: "OGN·100/298",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardLifecycleKeywordNames.Predict],
                    manaCost: 3,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-PROGRESS-GLORY"] = new(
                    "P1-UNIT-PROGRESS-GLORY",
                    cardNo: "SFD·075/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "机械"],
                    manaCost: 4,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-PROGRESS-GLORY-RECYCLE-001"] = new(
                    "P1-PROGRESS-GLORY-RECYCLE-001",
                    cardNo: "UNL-007/219",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-PROGRESS-GLORY-KEEP-001"] = new(
                    "P1-PROGRESS-GLORY-KEEP-001",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "SFD·075/221");
        var engine = new CoreRuleEngine();

        var playResult = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-gemstone-seer-static-granted-predict-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-PROGRESS-GLORY",
                "SFD·075/221",
                []),
            CancellationToken.None);
        Assert.True(playResult.Accepted, playResult.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-gemstone-seer-static-granted-predict-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-gemstone-seer-static-granted-predict-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var choice=Assert.IsType<PendingCardChoiceState>(p2Pass.State.PendingCardChoice);
        p2Pass=await OfficialInsightAndSpellLockTests.Act(p2Pass.State,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["P1-PROGRESS-GLORY-RECYCLE-001"]));
        Assert.Equal(
            ["P1-UNIT-OGN-GEMSTONE-SEER", "P1-UNIT-PROGRESS-GLORY"],
            p2Pass.State.PlayerZones["P1"].Base);
        var ruleTextAura = Assert.Single(p2Pass.State.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P1-UNIT-OGN-GEMSTONE-SEER", StringComparison.Ordinal));
        Assert.Equal("P1-UNIT-PROGRESS-GLORY", ruleTextAura.TargetObjectId);
        Assert.Equal("WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", ruleTextAura.Duration);
        Assert.Empty(ruleTextAura.EffectKind);
        Assert.Null(ruleTextAura.SourceCardNo);
        Assert.Empty(ruleTextAura.SourcePath);
        Assert.DoesNotContain(
            p2Pass.State.ContinuousEffects,
            effect => string.Equals(effect.SourceObjectId, "P1-UNIT-OGN-GEMSTONE-SEER", StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-UNIT-OGN-GEMSTONE-SEER", StringComparison.Ordinal));
        Assert.Equal(
            ["P1-PROGRESS-GLORY-KEEP-001", "P1-PROGRESS-GLORY-RECYCLE-001"],
            p2Pass.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(3, p2Pass.State.CardObjects["P1-UNIT-PROGRESS-GLORY"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, "机械"],
            p2Pass.State.CardObjects["P1-UNIT-PROGRESS-GLORY"].Tags);
        Assert.Contains(
            p2Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
                && string.Equals(
                    gameEvent.Payload["sourceObjectId"] as string,
                    "P1-UNIT-PROGRESS-GLORY",
                    StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(2, "P1-UNIT-UNL-BABBLING-PORO", "UNL-224/219", "P1-UNL-BABBLING-PORO-TOP-001", "P1-UNL-BABBLING-PORO-SECOND-001")]
    [InlineData(2, "P1-UNIT-BABBLING-PORO", "OGN·171/298", "P1-BABBLING-PORO-TOP-001", "P1-BABBLING-PORO-SECOND-001")]
    [InlineData(5, "P1-UNIT-GEMSTONE-GOLEM", "OGN·086/298", "P1-GEMSTONE-GOLEM-TOP-001", "P1-GEMSTONE-GOLEM-SECOND-001")]
    [InlineData(6, "P1-UNIT-DASE-SCOUT", "OGN·174/298", "P1-DASE-SCOUT-TOP-001", "P1-DASE-SCOUT-SECOND-001")]
    [InlineData(4, "P1-UNIT-JHIN", "UNL-089/219", "P1-JHIN-TOP-001", "P1-JHIN-SECOND-001")]
    [InlineData(4, "P1-UNIT-JHIN-A", "UNL-089a/219", "P1-JHIN-A-TOP-001", "P1-JHIN-A-SECOND-001")]
    public async Task CoreRuleEngineRejectsPredictSourceUnitWhenTargetIsOutsideTopCard(
        int mana,
        string sourceObjectId,
        string cardNo,
        string topObjectId,
        string secondObjectId)
    {
        var state = PunishmentState(mana) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId],
                    MainDeck = [topObjectId, secondObjectId]
                }
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-predict-unit-second-card", "P1", "PLAY_CARD"),
            new PlayCardCommand(sourceObjectId, cardNo, [secondObjectId]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal([sourceObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Equal([topObjectId, secondObjectId], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysForcedConscriptionControlSmallEnemyRecall()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-forced-conscription-control-small-enemy-recall.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P2-FORCED-CONSCRIPTION-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-FORCED-CONSCRIPTION-OTHER-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.True(result.FinalState.CardObjects["P2-FORCED-CONSCRIPTION-UNIT-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsForcedConscriptionWhenTargetPowerAboveThree()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-FORCED-CONSCRIPTION"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-FORCED-CONSCRIPTION-LARGE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-FORCED-CONSCRIPTION-LARGE-001"] = new(
                    "P2-FORCED-CONSCRIPTION-LARGE-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-forced-conscription-large-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-FORCED-CONSCRIPTION",
                "UNL-140/219",
                ["P2-FORCED-CONSCRIPTION-LARGE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-FORCED-CONSCRIPTION"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-FORCED-CONSCRIPTION-LARGE-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineForcedConscriptionResolutionSkipsAlreadyControlledEnemyZoneTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-FORCED-CONSCRIPTION-DIRTY-BATTLEFIELD",
                    "P1",
                    "P1-SPELL-FORCED-CONSCRIPTION",
                    "FORCED_CONSCRIPTION_CONTROL_SMALL_ENEMY_RECALL",
                    "UNL-140/219",
                    ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"] = new(
                    "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-forced-conscription-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-forced-conscription-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"], p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-SPELL-FORCED-CONSCRIPTION"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("P1", p2Pass.State.CardObjects["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"].ControllerId);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_CONTROL_GAINED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTakenForARideControlEnemyRecall()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-taken-for-a-ride-control-enemy-recall.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P2-TAKEN-FOR-A-RIDE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P2-TAKEN-FOR-A-RIDE-UNIT-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsTakenForARideWhenTargetIsNotUnit()
    {
        var state = PunishmentState(mana: 8) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-TAKEN-FOR-A-RIDE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-TAKEN-FOR-A-RIDE-EQUIPMENT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-TAKEN-FOR-A-RIDE-EQUIPMENT-001"] = new(
                    "P2-TAKEN-FOR-A-RIDE-EQUIPMENT-001",
                    tags: [CardObjectTags.EquipmentCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-taken-for-a-ride-equipment-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-TAKEN-FOR-A-RIDE",
                "OGN·203/298",
                ["P2-TAKEN-FOR-A-RIDE-EQUIPMENT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(8, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-TAKEN-FOR-A-RIDE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-TAKEN-FOR-A-RIDE-EQUIPMENT-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysBoneclubPromoEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-boneclub-promo-equipment.fixture.json",
            "P1-EQUIPMENT-BONECLUB-PROMO");

    [Fact]
    public Task CoreRuleEngineRejectsBoneclubPromoWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-BONECLUB-PROMO",
            "SFD·118a/221·P",
            "P1-BONECLUB-PROMO-BASE-UNIT-001");

    [Fact]
    public async Task P4BoneclubPromoTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-boneclub-promo-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BONECLUB-PROMO-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-BONECLUB-PROMO"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BONECLUB-PROMO-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BONECLUB-PROMO-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BONECLUB-PROMO-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BONECLUB-PROMO-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public Task CoreRuleEnginePlaysHextechGauntletEquipment() =>
        AssertSimpleEquipmentFixtureAsync(
            "p2-preflight-play-hextech-gauntlet-equipment.fixture.json",
            "P1-EQUIPMENT-HEXTECH-GAUNTLET", expectedTags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]);

    [Fact]
    public Task CoreRuleEngineRejectsHextechGauntletWhenTargetsAreProvided() =>
        AssertEquipmentWithTargetRejectedAsync(
            3,
            "P1-EQUIPMENT-HEXTECH-GAUNTLET",
            "UNL-188/219",
            "P1-HEXTECH-GAUNTLET-BASE-UNIT-001");

    [Fact]
    public async Task P4HextechGauntletTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-hextech-gauntlet-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HEXTECH-GAUNTLET-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-EQUIPMENT-HEXTECH-GAUNTLET"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-HEXTECH-GAUNTLET-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HEXTECH-GAUNTLET-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HEXTECH-GAUNTLET-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-HEXTECH-GAUNTLET-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTreasureGolemCreateFourGold()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-treasure-golem-create-four-gold.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P1-UNIT-TREASURE-GOLEM",
                "P1-UNIT-TREASURE-GOLEM-TOKEN-001",
                "P1-UNIT-TREASURE-GOLEM-TOKEN-002",
                "P1-UNIT-TREASURE-GOLEM-TOKEN-003",
                "P1-UNIT-TREASURE-GOLEM-TOKEN-004"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(9, result.FinalState.CardObjects["P1-UNIT-TREASURE-GOLEM"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-TREASURE-GOLEM"].Tags);
        foreach (var tokenObjectId in result.FinalState.PlayerZones["P1"].Base.Skip(1))
        {
            var token = result.FinalState.CardObjects[tokenObjectId];
            Assert.Equal([CardObjectTags.EquipmentCard, "反应", "金币"], token.Tags);
            Assert.Equal("SFD·T03", token.CardNo);
            Assert.True(token.IsExhausted);
        }
    }

    [Fact]
    public Task CoreRuleEngineRejectsTreasureGolemWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            8,
            "P1-UNIT-TREASURE-GOLEM",
            "SFD·174/221",
            "P1-TREASURE-GOLEM-BASE-UNIT-001");

    [Fact]
    public async Task P4TreasureGolemTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-treasure-golem-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(8, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-TREASURE-GOLEM-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-TREASURE-GOLEM"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TREASURE-GOLEM-BASE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-TREASURE-GOLEM-BASE-UNIT-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-TREASURE-GOLEM-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-TREASURE-GOLEM-BASE-UNIT-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFaithfulCraftsmanCreateMinion()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-faithful-craftsman-create-minion.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-UNIT-FAITHFUL-CRAFTSMAN", "P1-UNIT-FAITHFUL-CRAFTSMAN-TOKEN-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-FAITHFUL-CRAFTSMAN"].Power);
        Assert.DoesNotContain(CardObjectTags.MinionTokenFamily, result.FinalState.CardObjects["P1-UNIT-FAITHFUL-CRAFTSMAN"].Tags);
        Assert.Equal(1, result.FinalState.CardObjects["P1-UNIT-FAITHFUL-CRAFTSMAN-TOKEN-001"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.MinionTokenFamily, "随从"],
            result.FinalState.CardObjects["P1-UNIT-FAITHFUL-CRAFTSMAN-TOKEN-001"].Tags);
    }

    [Fact]
    public Task CoreRuleEngineRejectsFaithfulCraftsmanWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            3,
            "P1-UNIT-FAITHFUL-CRAFTSMAN",
            "OGN·211/298",
            "P1-FAITHFUL-CRAFTSMAN-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysRoyalGuardCreateSandSoldier()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-royal-guard-create-sand-soldier.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-UNIT-ROYAL-GUARD", "P1-UNIT-ROYAL-GUARD-TOKEN-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-ROYAL-GUARD"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-ROYAL-GUARD-TOKEN-001"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, "恕瑞玛", CardObjectTags.SandSoldier],
            result.FinalState.CardObjects["P1-UNIT-ROYAL-GUARD-TOKEN-001"].Tags);
    }

    [Fact]
    public Task CoreRuleEngineRejectsRoyalGuardWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            4,
            "P1-UNIT-ROYAL-GUARD",
            "SFD·157/221",
            "P1-ROYAL-GUARD-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysBlueflameGuardianPowerPlusEight()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-blueflame-guardian-power-plus-8.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-BLUEFLAME-TARGET-001", "P1-UNIT-BLUEFLAME-GUARDIAN"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(8, result.FinalState.CardObjects["P1-UNIT-BLUEFLAME-GUARDIAN"].Power);
        Assert.Equal(10, result.FinalState.CardObjects["P1-BLUEFLAME-TARGET-001"].Power);
        Assert.Equal(8, result.FinalState.CardObjects["P1-BLUEFLAME-TARGET-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public Task CoreRuleEngineRejectsBlueflameGuardianWhenTargetIsNotUnit() =>
        AssertSourceUnitNonUnitTargetRejectedAsync(
            8,
            "P1-UNIT-BLUEFLAME-GUARDIAN",
            "OGN·082/298",
            "P1-BLUEFLAME-EQUIPMENT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysBlastconeSproutPowerMinusTwoFloor()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-blastcone-sprout-power-minus-2-floor.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-BLASTCONE-SPROUT"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-BLASTCONE-SPROUT"].Power);
        var targetState = result.FinalState.CardObjects["P2-BLASTCONE-TARGET-001"];
        Assert.Equal(1, targetState.Power);
        Assert.Equal(-1, targetState.UntilEndOfTurnPowerModifier);
        var modifier = Assert.Single(targetState.UntilEndOfTurnPowerModifiers);
        Assert.Equal(-1, modifier.PowerDelta);
        Assert.Equal(-2, modifier.RequestedPowerDelta);
        Assert.Equal(1, modifier.MinimumPower);
        Assert.Equal(1, modifier.ResultingPower);
        Assert.Equal(2, modifier.BasePower);
        Assert.Equal(1, modifier.EffectivePower);
        Assert.Equal(1, modifier.AppliedOrder);

        var powerEffect = Assert.Single(
            result.FinalState.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P2-BLASTCONE-TARGET-001", StringComparison.Ordinal));
        Assert.Equal(-1, powerEffect.PowerDelta);
        Assert.Equal(-2, powerEffect.RequestedPowerDelta);
        Assert.Equal(-1, powerEffect.AppliedPowerDelta);
        Assert.Equal(1, powerEffect.MinimumPower);
        Assert.Equal(1, powerEffect.ResultingPower);
        Assert.Equal(2, powerEffect.BasePower);
        Assert.Equal(1, powerEffect.EffectivePower);
        Assert.Equal(1, powerEffect.AppliedOrder);

        var snapshot = ResolutionResult.BuildSnapshots(result.FinalState)["P1"];
        var continuousEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            snapshot.Timing["continuousEffects"]);
        var powerEffectView = Assert.Single(
            continuousEffects,
            effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect["targetObjectId"] as string, "P2-BLASTCONE-TARGET-001", StringComparison.Ordinal));
        Assert.Equal(-1, Assert.IsType<int>(powerEffectView["powerDelta"]));
        Assert.Equal(-2, Assert.IsType<int>(powerEffectView["requestedPowerDelta"]));
        Assert.Equal(-1, Assert.IsType<int>(powerEffectView["appliedPowerDelta"]));
        Assert.Equal(1, Assert.IsType<int>(powerEffectView["minimumPower"]));
        Assert.Equal(1, Assert.IsType<int>(powerEffectView["resultingPower"]));
        Assert.Equal(1, Assert.IsType<int>(powerEffectView["appliedOrder"]));
        Assert.Equal("FOUNDATION_ONLY", Assert.IsType<string>(powerEffectView["layerEngineStatus"]));
    }

    [Fact]
    public Task CoreRuleEngineRejectsBlastconeSproutWhenTargetIsNotUnit() =>
        AssertSourceUnitNonUnitTargetRejectedAsync(
            2,
            "P1-UNIT-BLASTCONE-SPROUT",
            "OGN·097/298",
            "P1-BLASTCONE-SPROUT-EQUIPMENT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysProwlingHunterCreateWarhawk()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-prowling-hunter-create-warhawk.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-UNIT-PROWLING-HUNTER", "P1-UNIT-PROWLING-HUNTER-TOKEN-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-PROWLING-HUNTER"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P1-UNIT-PROWLING-HUNTER-TOKEN-001"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Spellshield, "鸟类"],
            result.FinalState.CardObjects["P1-UNIT-PROWLING-HUNTER-TOKEN-001"].Tags);
    }

    [Fact]
    public Task CoreRuleEngineRejectsProwlingHunterWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            4,
            "P1-UNIT-PROWLING-HUNTER",
            "UNL-033/219",
            "P1-PROWLING-HUNTER-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysApprenticeEngineerReturnGraveyardEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-apprentice-engineer-return-graveyard-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-APPRENTICE-ENGINEER"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-EQUIPMENT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-APPRENTICE-ENGINEER"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-APPRENTICE-ENGINEER"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsApprenticeEngineerWhenGraveyardTargetIsNotEquipment()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-APPRENTICE-ENGINEER"],
                    Graveyard = ["P1-GRAVE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-UNIT-001"] = new(
                    "P1-GRAVE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-apprentice-engineer-unit-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-APPRENTICE-ENGINEER",
                "SFD·061/221",
                ["P1-GRAVE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-APPRENTICE-ENGINEER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-UNIT-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDarkenedLurkerDiscardDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-darkened-lurker-discard-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-DARKENED-LURKER"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-DARKENED-LURKER-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-DARKENED-LURKER-DISCARD-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-DARKENED-LURKER"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-DARKENED-LURKER"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineDarkenedLurkerResolutionSkipsOpponentControlledHandTarget()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 3) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-DARKENED-LURKER-DIRTY-HAND",
                    "P1",
                    "P1-UNIT-DARKENED-LURKER",
                    "DARKENED_LURKER_PLAY_UNIT_DISCARD_DRAW",
                    "UNL-123/219",
                    ["P1-DARKENED-LURKER-DIRTY-P2-HAND-CARD"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-DARKENED-LURKER-DRAW-001"],
                    Hand =
                    [
                        "P1-DARKENED-LURKER-DISCARD-001",
                        "P1-DARKENED-LURKER-DIRTY-P2-HAND-CARD"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-DARKENED-LURKER"] = new(
                    "P1-UNIT-DARKENED-LURKER",
                    cardNo: "UNL-123/219",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-DARKENED-LURKER-DISCARD-001"] = new(
                    "P1-DARKENED-LURKER-DISCARD-001",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-DARKENED-LURKER-DIRTY-P2-HAND-CARD"] = new(
                    "P1-DARKENED-LURKER-DIRTY-P2-HAND-CARD",
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-darkened-lurker-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-darkened-lurker-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P1-UNIT-DARKENED-LURKER"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P1-DARKENED-LURKER-DISCARD-001", "P1-DARKENED-LURKER-DIRTY-P2-HAND-CARD", "P1-DARKENED-LURKER-DRAW-001"],
            p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playerId"] as string, "P1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDarkenedLurkerWhenDiscardTargetIsSource()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-DARKENED-LURKER"]
                }
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-darkened-lurker-source-discard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-DARKENED-LURKER",
                "UNL-123/219",
                ["P1-UNIT-DARKENED-LURKER"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-DARKENED-LURKER"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysShepherdDogReturnGraveyardUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-shepherd-dog-return-graveyard-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SHEPHERD-DOG"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-EQUIPMENT-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-SHEPHERD-DOG"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-SHEPHERD-DOG"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsShepherdDogWhenGraveyardTargetIsNotUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-SHEPHERD-DOG"],
                    Graveyard = ["P1-GRAVE-EQUIPMENT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-EQUIPMENT-001"] = new(
                    "P1-GRAVE-EQUIPMENT-001",
                    tags: [CardObjectTags.EquipmentCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-shepherd-dog-equipment-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SHEPHERD-DOG",
                "OGN·165/298",
                ["P1-GRAVE-EQUIPMENT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-SHEPHERD-DOG"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-EQUIPMENT-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysAnnieReturnGraveyardSpell()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-annie-return-graveyard-spell.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-ANNIE"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-SPELL-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-ANNIE"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-ANNIE"].Tags);
        Assert.Equal([CardObjectTags.SpellCard], result.FinalState.CardObjects["P1-GRAVE-SPELL-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsAnnieWhenGraveyardTargetIsNotSpell()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-ANNIE"],
                    Graveyard = ["P1-GRAVE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-UNIT-001"] = new(
                    "P1-GRAVE-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-annie-unit-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-ANNIE",
                "OGS·010/024",
                ["P1-GRAVE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-ANNIE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-UNIT-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysScuttleCrabDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-scuttle-crab-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SCUTTLE-CRAB"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-SCUTTLE-CRAB-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-SCUTTLE-CRAB"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-SCUTTLE-CRAB"].Tags);
    }

    [Fact]
    public Task CoreRuleEngineRejectsScuttleCrabWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            2,
            "P1-UNIT-SCUTTLE-CRAB",
            "UNL-053/219",
            "P1-SCUTTLE-CRAB-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysYordleInstructorDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-yordle-instructor-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-YORDLE-INSTRUCTOR"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-YORDLE-INSTRUCTOR-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-YORDLE-INSTRUCTOR"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Bulwark],
            result.FinalState.CardObjects["P1-UNIT-YORDLE-INSTRUCTOR"].Tags);
    }

    [Fact]
    public Task CoreRuleEngineRejectsYordleInstructorWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            3,
            "P1-UNIT-YORDLE-INSTRUCTOR",
            "OGN·087/298",
            "P1-YORDLE-INSTRUCTOR-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysSpriteMotherCreateSprite()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sprite-mother-create-sprite.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-UNIT-SPRITE-MOTHER", "P1-UNIT-SPRITE-MOTHER-TOKEN-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-SPRITE-MOTHER"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-SPRITE-MOTHER"].Tags);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-SPRITE-MOTHER-TOKEN-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard, "仙灵", CardObjectTags.Ephemeral], result.FinalState.CardObjects["P1-UNIT-SPRITE-MOTHER-TOKEN-001"].Tags);
    }

    [Fact]
    public Task CoreRuleEngineRejectsSpriteMotherWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            4,
            "P1-UNIT-SPRITE-MOTHER",
            "OGN·106/298",
            "P1-SPRITE-MOTHER-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysMegasharkCannonDamageEnemyBattlefield()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-megashark-cannon-damage-enemy-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-MEGASHARK-CANNON"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLEFIELD-MEGASHARK-TARGET-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(6, result.FinalState.CardObjects["P1-UNIT-MEGASHARK-CANNON"].Power);
        Assert.Equal(6, result.FinalState.CardObjects["P2-BATTLEFIELD-MEGASHARK-TARGET-001"].Damage);
        Assert.Equal(7, result.FinalState.CardObjects["P2-BATTLEFIELD-MEGASHARK-TARGET-001"].Power);
        Assert.Contains("DAMAGE_APPLIED", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMegasharkCannonWhenTargetIsFriendlyUnit()
    {
        var state = PunishmentState(mana: 6) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-MEGASHARK-CANNON"],
                    Battlefields = ["P1-BATTLEFIELD-MEGASHARK-FRIENDLY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MEGASHARK-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-MEGASHARK-FRIENDLY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-megashark-cannon-friendly-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-MEGASHARK-CANNON",
                "OGN·092/298",
                ["P1-BATTLEFIELD-MEGASHARK-FRIENDLY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(6, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-MEGASHARK-CANNON"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-MEGASHARK-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCarnivorousVinetendrilSourceTargetPowerDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-carnivorous-vinetendril-source-target-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-CARNIVOROUS-VINETENDRIL"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-VINETENDRIL-TARGET-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-CARNIVOROUS-VINETENDRIL"].Damage);
        Assert.Equal(6, result.FinalState.CardObjects["P1-UNIT-CARNIVOROUS-VINETENDRIL"].Power);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCarnivorousVinetendrilWhenTargetIsFriendlyUnit()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-CARNIVOROUS-VINETENDRIL"],
                    Battlefields = ["P1-BATTLEFIELD-VINETENDRIL-FRIENDLY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-VINETENDRIL-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-VINETENDRIL-FRIENDLY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-vinetendril-friendly-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CARNIVOROUS-VINETENDRIL",
                "OGN·149/298",
                ["P1-BATTLEFIELD-VINETENDRIL-FRIENDLY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-CARNIVOROUS-VINETENDRIL"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLEFIELD-VINETENDRIL-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineCarnivorousVinetendrilResolutionSkipsOpponentControlledEnemyZoneTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-VINETENDRIL-DIRTY-POWER-DAMAGE",
                    "P1",
                    "P1-UNIT-CARNIVOROUS-VINETENDRIL",
                    "CARNIVOROUS_VINETENDRIL_SOURCE_TARGET_POWER_DAMAGE",
                    "OGN·149/298",
                    ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-UNIT-CARNIVOROUS-VINETENDRIL"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-CARNIVOROUS-VINETENDRIL"] = new(
                    "P1-UNIT-CARNIVOROUS-VINETENDRIL",
                    cardNo: "OGN·149/298",
                    manaCost: 5,
                    power: 6,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"] = new(
                    "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-vinetendril-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-vinetendril-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P1-UNIT-CARNIVOROUS-VINETENDRIL"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"], p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, p2Pass.State.CardObjects["P1-UNIT-CARNIVOROUS-VINETENDRIL"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"].Damage);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysColdBloodedAristocratDestroyFriendlyUnitAdditionalCost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-cold-blooded-aristocrat-destroy-friendly-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-COLD-BLOODED-ARISTOCRAT"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-COLD-BLOODED-COST-UNIT-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(6, result.FinalState.CardObjects["P1-UNIT-COLD-BLOODED-ARISTOCRAT"].Power);
        Assert.DoesNotContain("P1-COLD-BLOODED-COST-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsColdBloodedAristocratWithoutAdditionalCost()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-COLD-BLOODED-ARISTOCRAT"],
                    Base = ["P1-COLD-BLOODED-COST-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-COLD-BLOODED-COST-UNIT-001"] = new(
                    "P1-COLD-BLOODED-COST-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-cold-blooded-aristocrat-missing-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-UNIT-COLD-BLOODED-ARISTOCRAT", "OGN·208/298", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-COLD-BLOODED-ARISTOCRAT"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-COLD-BLOODED-COST-UNIT-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsColdBloodedAristocratWhenAdditionalCostTargetsEquipment()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-COLD-BLOODED-ARISTOCRAT"],
                    Base = ["P1-COLD-BLOODED-COST-EQUIPMENT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-COLD-BLOODED-COST-EQUIPMENT-001"] = new(
                    "P1-COLD-BLOODED-COST-EQUIPMENT-001",
                    tags: [CardObjectTags.EquipmentCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-cold-blooded-aristocrat-equipment-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-COLD-BLOODED-ARISTOCRAT",
                "OGN·208/298",
                [],
                OptionalCosts: ["DESTROY_FRIENDLY_UNIT:P1-COLD-BLOODED-COST-EQUIPMENT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-COLD-BLOODED-ARISTOCRAT"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-COLD-BLOODED-COST-EQUIPMENT-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysLegionQuartermasterReturnFriendlyEquipmentAdditionalCost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-legion-quartermaster-return-friendly-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-LEGION-QUARTERMASTER"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-LEGION-QUARTERMASTER-COST-EQUIPMENT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-LEGION-QUARTERMASTER"].Power);
        Assert.Contains("崔法利", result.FinalState.CardObjects["P1-UNIT-LEGION-QUARTERMASTER"].Tags);
        Assert.DoesNotContain("P1-LEGION-QUARTERMASTER-COST-EQUIPMENT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsLegionQuartermasterWithoutAdditionalCost()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-LEGION-QUARTERMASTER"],
                    Base = ["P1-LEGION-QUARTERMASTER-COST-EQUIPMENT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGION-QUARTERMASTER-COST-EQUIPMENT-001"] = new(
                    "P1-LEGION-QUARTERMASTER-COST-EQUIPMENT-001",
                    tags: [CardObjectTags.EquipmentCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-legion-quartermaster-missing-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-UNIT-LEGION-QUARTERMASTER", "SFD·044/221", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-LEGION-QUARTERMASTER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-LEGION-QUARTERMASTER-COST-EQUIPMENT-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsLegionQuartermasterWhenAdditionalCostTargetsUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-LEGION-QUARTERMASTER"],
                    Base = ["P1-LEGION-QUARTERMASTER-COST-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGION-QUARTERMASTER-COST-UNIT-001"] = new(
                    "P1-LEGION-QUARTERMASTER-COST-UNIT-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-legion-quartermaster-unit-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-LEGION-QUARTERMASTER",
                "SFD·044/221",
                [],
                OptionalCosts: ["RETURN_FRIENDLY_EQUIPMENT:P1-LEGION-QUARTERMASTER-COST-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-LEGION-QUARTERMASTER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-LEGION-QUARTERMASTER-COST-UNIT-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsLegionQuartermasterWhenAdditionalCostTargetsEnemyEquipment()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-LEGION-QUARTERMASTER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-LEGION-QUARTERMASTER-COST-EQUIPMENT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-LEGION-QUARTERMASTER-COST-EQUIPMENT-001"] = new(
                    "P2-LEGION-QUARTERMASTER-COST-EQUIPMENT-001",
                    tags: [CardObjectTags.EquipmentCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-legion-quartermaster-enemy-equipment-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-LEGION-QUARTERMASTER",
                "SFD·044/221",
                [],
                OptionalCosts: ["RETURN_FRIENDLY_EQUIPMENT:P2-LEGION-QUARTERMASTER-COST-EQUIPMENT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-LEGION-QUARTERMASTER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-LEGION-QUARTERMASTER-COST-EQUIPMENT-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysQuicksandMageDestroySmallEnemy()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-quicksand-mage-destroy-small-enemy.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-QUICKSAND-MAGE"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BASE-QUICKSAND-TARGET-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(6, result.FinalState.CardObjects["P1-UNIT-QUICKSAND-MAGE"].Power);
        Assert.DoesNotContain("P2-BASE-QUICKSAND-TARGET-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsQuicksandMageWhenEnemyPowerIsTooHigh()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-QUICKSAND-MAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-QUICKSAND-LARGE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-QUICKSAND-LARGE-001"] = new(
                    "P2-BASE-QUICKSAND-LARGE-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-quicksand-mage-large-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-QUICKSAND-MAGE",
                "SFD·158/221",
                ["P2-BASE-QUICKSAND-LARGE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-QUICKSAND-MAGE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-QUICKSAND-LARGE-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysZaunBodyguardReturnBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-zaun-bodyguard-return-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-ZAUN-BODYGUARD"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P2-HAND-001", "P2-BATTLEFIELD-ZAUN-TARGET-001"],
            result.FinalState.PlayerZones["P2"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-ZAUN-BODYGUARD"].Power);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsZaunBodyguardWhenTargetIsInBase()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-ZAUN-BODYGUARD"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-ZAUN-TARGET-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-ZAUN-TARGET-001"] = new(
                    "P2-BASE-ZAUN-TARGET-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-zaun-bodyguard-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-ZAUN-BODYGUARD",
                "OGN·188/298",
                ["P2-BASE-ZAUN-TARGET-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-ZAUN-BODYGUARD"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-ZAUN-TARGET-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDragonCavalryDestroyEnemyUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dragon-cavalry-destroy-enemy-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-DRAGON-CAVALRY"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BASE-DRAGON-CAVALRY-TARGET-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(6, result.FinalState.CardObjects["P1-UNIT-DRAGON-CAVALRY"].Power);
        Assert.DoesNotContain("P2-BASE-DRAGON-CAVALRY-TARGET-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDragonCavalryWhenTargetIsFriendlyUnit()
    {
        var state = PunishmentState(mana: 8) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-DRAGON-CAVALRY"],
                    Base = ["P1-BASE-DRAGON-CAVALRY-FRIENDLY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-DRAGON-CAVALRY-FRIENDLY-001"] = new(
                    "P1-BASE-DRAGON-CAVALRY-FRIENDLY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-dragon-cavalry-friendly-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-DRAGON-CAVALRY",
                "OGN·234/298",
                ["P1-BASE-DRAGON-CAVALRY-FRIENDLY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(8, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-DRAGON-CAVALRY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-DRAGON-CAVALRY-FRIENDLY-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFirstMateReadyAnotherUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-first-mate-ready-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-FIRST-MATE"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLEFIELD-FIRST-MATE-TARGET-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-FIRST-MATE"].Power);
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-FIRST-MATE-TARGET-001"].IsExhausted);
        Assert.Contains("UNIT_READIED", result.EventKinds);
    }

    [Fact]
    public Task CoreRuleEngineRejectsFirstMateWhenTargetIsEquipment() =>
        AssertSourceUnitNonUnitTargetRejectedAsync(
            3,
            "P1-UNIT-FIRST-MATE",
            "OGN·132/298",
            "P1-FIRST-MATE-EQUIPMENT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysArenaRookieGrantBoonFriendlyUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-arena-rookie-grant-boon.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-BASE-ARENA-ROOKIE-TARGET-001", "P1-UNIT-ARENA-ROOKIE"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-ARENA-ROOKIE"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-ARENA-ROOKIE-TARGET-001"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Boon],
            result.FinalState.CardObjects["P1-BASE-ARENA-ROOKIE-TARGET-001"].Tags);
        Assert.Contains("BOON_GRANTED", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsArenaRookieWhenTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-ARENA-ROOKIE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-ARENA-ROOKIE-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-ARENA-ROOKIE-ENEMY-001"] = new(
                    "P2-BASE-ARENA-ROOKIE-ENEMY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-arena-rookie-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-ARENA-ROOKIE",
                "OGN·136/298",
                ["P2-BASE-ARENA-ROOKIE-ENEMY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-ARENA-ROOKIE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-ARENA-ROOKIE-ENEMY-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsArenaRookieWhenFriendlyTargetIsOpponentControlled()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-ARENA-ROOKIE"],
                    Base = ["P1-DIRTY-OPPONENT-CONTROLLED-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-ARENA-ROOKIE"] = new(
                    "P1-UNIT-ARENA-ROOKIE",
                    cardNo: "OGN·136/298",
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
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-arena-rookie-opponent-controlled-friendly-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-ARENA-ROOKIE",
                "OGN·136/298",
                ["P1-DIRTY-OPPONENT-CONTROLLED-TARGET"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-ARENA-ROOKIE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-DIRTY-OPPONENT-CONTROLLED-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSwordVagrantDestroyEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sword-vagrant-destroy-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SWORD-VAGRANT"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BASE-SWORD-VAGRANT-EQUIPMENT-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-SWORD-VAGRANT"].Power);
        Assert.DoesNotContain("P2-BASE-SWORD-VAGRANT-EQUIPMENT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineAllowsSwordVagrantWithoutEquipmentTarget()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-SWORD-VAGRANT"]
                }
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "SFD·032/221");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sword-vagrant-no-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SWORD-VAGRANT",
                "SFD·032/221",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Null(result.State.PendingCardChoice);
        Assert.Empty(result.State.StackItems);
        Assert.Single(result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSwordVagrantWhenTargetIsUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-SWORD-VAGRANT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-SWORD-VAGRANT-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-SWORD-VAGRANT-UNIT-001"] = new(
                    "P2-BATTLEFIELD-SWORD-VAGRANT-UNIT-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sword-vagrant-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SWORD-VAGRANT",
                "SFD·032/221",
                ["P2-BATTLEFIELD-SWORD-VAGRANT-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-SWORD-VAGRANT"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BATTLEFIELD-SWORD-VAGRANT-UNIT-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSunShieldguardStunUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sun-shieldguard-stun-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SUN-SHIELDGUARD"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-SUN-SHIELDGUARD"].Power);
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P2-BASE-SUN-SHIELDGUARD-TARGET-001"].UntilEndOfTurnEffects);
        Assert.Contains("STATUS_EFFECT_APPLIED", result.EventKinds);
    }

    [Fact]
    public Task CoreRuleEngineRejectsSunShieldguardWhenTargetIsEquipment() =>
        AssertSourceUnitNonUnitTargetRejectedAsync(
            3,
            "P1-UNIT-SUN-SHIELDGUARD",
            "OGN·051/298",
            "P1-SUN-SHIELDGUARD-EQUIPMENT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysThunderclawUrsineCallRune()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-thunderclaw-ursine-call-rune.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-RUNE-002"], result.FinalState.PlayerZones["P1"].RuneDeck);
        Assert.Equal(["P1-UNIT-THUNDERCLAW-URSINE", "P1-RUNE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(6, result.FinalState.CardObjects["P1-UNIT-THUNDERCLAW-URSINE"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Bulwark],
            result.FinalState.CardObjects["P1-UNIT-THUNDERCLAW-URSINE"].Tags);
        Assert.True(result.FinalState.CardObjects["P1-RUNE-001"].IsExhausted);
        Assert.Contains("RUNES_CALLED", result.EventKinds);
    }

    [Fact]
    public Task CoreRuleEngineRejectsThunderclawUrsineWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            7,
            "P1-UNIT-THUNDERCLAW-URSINE",
            "OGN·137/298",
            "P1-THUNDERCLAW-URSINE-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysKinkouMonkGrantTwoBoons()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-kinkou-monk-grant-two-boons.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-BASE-KINKOU-MONK-TARGET-001", "P1-UNIT-KINKOU-MONK"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-KINKOU-MONK"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-BASE-KINKOU-MONK-TARGET-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BATTLEFIELD-KINKOU-MONK-TARGET-002"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Boon],
            result.FinalState.CardObjects["P1-BASE-KINKOU-MONK-TARGET-001"].Tags);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Boon],
            result.FinalState.CardObjects["P1-BATTLEFIELD-KINKOU-MONK-TARGET-002"].Tags);
        Assert.Equal(2, result.EventKinds.Count(kind => string.Equals(kind, "BOON_GRANTED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsKinkouMonkWhenSecondTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-KINKOU-MONK"],
                    Base = ["P1-BASE-KINKOU-MONK-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-KINKOU-MONK-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-KINKOU-MONK-FRIENDLY-001"] = new(
                    "P1-BASE-KINKOU-MONK-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-BASE-KINKOU-MONK-ENEMY-001"] = new(
                    "P2-BASE-KINKOU-MONK-ENEMY-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-kinkou-monk-enemy-second-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-KINKOU-MONK",
                "OGN·141/298",
                ["P1-BASE-KINKOU-MONK-FRIENDLY-001", "P2-BASE-KINKOU-MONK-ENEMY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-KINKOU-MONK"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysGloomyApothecaryReturnFriendlyBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-gloomy-apothecary-return-friendly-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-GLOOMY-APOTHECARY"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-GLOOMY-APOTHECARY-TARGET-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-GLOOMY-APOTHECARY"].Power);
        Assert.DoesNotContain("P1-BATTLEFIELD-GLOOMY-APOTHECARY-TARGET-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineAllowsGloomyApothecaryWithoutReturnTarget()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-GLOOMY-APOTHECARY"]
                }
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-gloomy-apothecary-no-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-GLOOMY-APOTHECARY",
                "UNL-021/219",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Null(result.State.PendingCardChoice);
        Assert.Empty(result.State.StackItems);
        Assert.Single(result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsGloomyApothecaryWhenTargetIsEnemyBattlefieldUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-GLOOMY-APOTHECARY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-GLOOMY-APOTHECARY-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-GLOOMY-APOTHECARY-ENEMY-001"] = new(
                    "P2-BATTLEFIELD-GLOOMY-APOTHECARY-ENEMY-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-gloomy-apothecary-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-GLOOMY-APOTHECARY",
                "UNL-021/219",
                ["P2-BATTLEFIELD-GLOOMY-APOTHECARY-ENEMY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-GLOOMY-APOTHECARY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BATTLEFIELD-GLOOMY-APOTHECARY-ENEMY-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysWindsongWingReturnSmallBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-windsong-wing-return-small-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-WINDSONG-WING"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P2-HAND-001", "P2-BATTLEFIELD-WINDSONG-WING-TARGET-001"],
            result.FinalState.PlayerZones["P2"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(1, result.FinalState.CardObjects["P1-UNIT-WINDSONG-WING"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Standby],
            result.FinalState.CardObjects["P1-UNIT-WINDSONG-WING"].Tags);
        Assert.DoesNotContain("P2-BATTLEFIELD-WINDSONG-WING-TARGET-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineAllowsWindsongWingWithoutReturnTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-WINDSONG-WING"]
                }
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-windsong-wing-no-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-WINDSONG-WING",
                "SFD·138/221",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Null(result.State.PendingCardChoice);
        Assert.Empty(result.State.StackItems);
        Assert.Single(result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsWindsongWingWhenBattlefieldTargetPowerIsTooHigh()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-WINDSONG-WING"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-WINDSONG-WING-LARGE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-WINDSONG-WING-LARGE-001"] = new(
                    "P2-BATTLEFIELD-WINDSONG-WING-LARGE-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-windsong-wing-large-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-WINDSONG-WING",
                "SFD·138/221",
                ["P2-BATTLEFIELD-WINDSONG-WING-LARGE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-WINDSONG-WING"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BATTLEFIELD-WINDSONG-WING-LARGE-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHexcoreDisruptorGrantRoam()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hexcore-disruptor-roam-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-HEXCORE-DISRUPTOR"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-HEXCORE-DISRUPTOR"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-HEXCORE-DISRUPTOR"].Tags);
        Assert.Equal(
            ["ROAM"],
            result.FinalState.CardObjects["P2-BATTLEFIELD-HEXCORE-DISRUPTOR-TARGET-001"].UntilEndOfTurnEffects);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "STATUS_EFFECT_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public Task CoreRuleEngineRejectsHexcoreDisruptorWhenTargetIsEquipment() =>
        AssertSourceUnitNonUnitTargetRejectedAsync(
            2,
            "P1-UNIT-HEXCORE-DISRUPTOR",
            "SFD·007/221",
            "P1-HEXCORE-DISRUPTOR-EQUIPMENT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysKadregrinDrawPowerfulUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-kadregrin-draw-powerful-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-BASE-KADREGRIN-POWERFUL-001", "P1-UNIT-KADREGRIN"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-KADREGRIN-DRAW-001", "P1-KADREGRIN-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(9, result.FinalState.CardObjects["P1-UNIT-KADREGRIN"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-KADREGRIN"].Tags);
        Assert.Contains("CARD_DRAWN", result.EventKinds);
    }

    [Fact]
    public Task CoreRuleEngineRejectsKadregrinWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            9,
            "P1-UNIT-KADREGRIN",
            "OGN·038/298",
            "P1-KADREGRIN-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysFrenziedRaiderMoveBattlefieldUnitToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-frenzied-raider-move-battlefield-unit-to-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-FRENZIED-RAIDER"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P2-BASE-FRENZIED-RAIDER-UNIT-001", "P2-BATTLEFIELD-FRENZIED-RAIDER-TARGET-001"],
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-FRENZIED-RAIDER"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardObjectTags.Bulwark],
            result.FinalState.CardObjects["P1-UNIT-FRENZIED-RAIDER"].Tags);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFrenziedRaiderWhenTargetIsInBase()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-FRENZIED-RAIDER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-FRENZIED-RAIDER-TARGET-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-FRENZIED-RAIDER-TARGET-001"] = new(
                    "P2-BASE-FRENZIED-RAIDER-TARGET-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-frenzied-raider-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-FRENZIED-RAIDER",
                "OGN·191/298",
                ["P2-BASE-FRENZIED-RAIDER-TARGET-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-FRENZIED-RAIDER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-FRENZIED-RAIDER-TARGET-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysAbyssalBehemothReturnFriendlyAndEnemy()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-abyssal-behemoth-return-friendly-and-enemy.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-ABYSSAL-BEHEMOTH"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BASE-ABYSSAL-BEHEMOTH-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BATTLEFIELD-ABYSSAL-BEHEMOTH-ENEMY-001"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(8, result.FinalState.CardObjects["P1-UNIT-ABYSSAL-BEHEMOTH"].Power);
        Assert.DoesNotContain("P1-BASE-ABYSSAL-BEHEMOTH-FRIENDLY-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BATTLEFIELD-ABYSSAL-BEHEMOTH-ENEMY-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsAbyssalBehemothWhenTargetsAreReversed()
    {
        var state = PunishmentState(mana: 7) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-ABYSSAL-BEHEMOTH"],
                    Base = ["P1-BASE-ABYSSAL-BEHEMOTH-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-ABYSSAL-BEHEMOTH-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-ABYSSAL-BEHEMOTH-FRIENDLY-001"] = new(
                    "P1-BASE-ABYSSAL-BEHEMOTH-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-BATTLEFIELD-ABYSSAL-BEHEMOTH-ENEMY-001"] = new(
                    "P2-BATTLEFIELD-ABYSSAL-BEHEMOTH-ENEMY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-abyssal-behemoth-reversed-targets", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-ABYSSAL-BEHEMOTH",
                "SFD·132/221",
                ["P2-BATTLEFIELD-ABYSSAL-BEHEMOTH-ENEMY-001", "P1-BASE-ABYSSAL-BEHEMOTH-FRIENDLY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(7, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-ABYSSAL-BEHEMOTH"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-ABYSSAL-BEHEMOTH-FRIENDLY-001"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLEFIELD-ABYSSAL-BEHEMOTH-ENEMY-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBubblebotReadyFriendlyMechanical()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-bubblebot-ready-friendly-mechanical.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-BASE-BUBBLEBOT-MECHANICAL-001", "P1-UNIT-BUBBLEBOT"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-BUBBLEBOT"].Power);
        Assert.False(result.FinalState.CardObjects["P1-BASE-BUBBLEBOT-MECHANICAL-001"].IsExhausted);
        Assert.Contains("UNIT_READIED", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBubblebotWhenTargetIsNotMechanical()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-BUBBLEBOT"],
                    Base = ["P1-BASE-BUBBLEBOT-NON-MECHANICAL-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-BUBBLEBOT-NON-MECHANICAL-001"] = new(
                    "P1-BASE-BUBBLEBOT-NON-MECHANICAL-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-bubblebot-non-mechanical-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-BUBBLEBOT",
                "SFD·062/221",
                ["P1-BASE-BUBBLEBOT-NON-MECHANICAL-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-BUBBLEBOT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSpriteQueenCreateSprite()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sprite-queen-create-sprite.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-UNIT-SPRITE-QUEEN", "P1-UNIT-SPRITE-QUEEN-TOKEN-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(6, result.FinalState.CardObjects["P1-UNIT-SPRITE-QUEEN"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-SPRITE-QUEEN-TOKEN-001"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, "仙灵", CardObjectTags.Ephemeral],
            result.FinalState.CardObjects["P1-UNIT-SPRITE-QUEEN-TOKEN-001"].Tags);
        Assert.Contains("UNIT_TOKEN_CREATED", result.EventKinds);
    }

    [Fact]
    public Task CoreRuleEngineRejectsSpriteQueenWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            7,
            "P1-UNIT-SPRITE-QUEEN",
            "UNL-084/219",
            "P1-SPRITE-QUEEN-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysFaerieDragonGrantFourBoons()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-faerie-dragon-grant-four-boons.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P1-BASE-FAERIE-DRAGON-TARGET-001",
                "P1-BASE-FAERIE-DRAGON-TARGET-002",
                "P1-UNIT-FAERIE-DRAGON"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(7, result.FinalState.CardObjects["P1-UNIT-FAERIE-DRAGON"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-BASE-FAERIE-DRAGON-TARGET-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-FAERIE-DRAGON-TARGET-002"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P1-BATTLEFIELD-FAERIE-DRAGON-TARGET-003"].Power);
        Assert.Equal(6, result.FinalState.CardObjects["P1-BATTLEFIELD-FAERIE-DRAGON-TARGET-004"].Power);
        Assert.Equal(4, result.EventKinds.Count(kind => string.Equals(kind, "BOON_GRANTED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineAllowsFaerieDragonWithoutTargets()
    {
        var state = PunishmentState(mana: 7) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-FAERIE-DRAGON"]
                }
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "SFD·101/221");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-faerie-dragon-no-targets", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-FAERIE-DRAGON",
                "SFD·101/221",
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Empty(stackItem.TargetObjectIds);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFaerieDragonWhenTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 7) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-FAERIE-DRAGON"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-FAERIE-DRAGON-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-FAERIE-DRAGON-ENEMY-001"] = new(
                    "P2-BASE-FAERIE-DRAGON-ENEMY-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-faerie-dragon-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-FAERIE-DRAGON",
                "SFD·101/221",
                ["P2-BASE-FAERIE-DRAGON-ENEMY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(7, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-FAERIE-DRAGON"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-FAERIE-DRAGON-ENEMY-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Theory]
    [InlineData("p2-preflight-play-ezreal-discard-draw-two.fixture.json", "P1-UNIT-EZREAL", "P1-EZREAL-DISCARD-001", "P1-EZREAL-DRAW-001", "P1-EZREAL-DRAW-002")]
    [InlineData("p2-preflight-play-ezreal-alt-a-discard-draw-two.fixture.json", "P1-UNIT-EZREAL-A", "P1-EZREAL-A-DISCARD-001", "P1-EZREAL-A-DRAW-001", "P1-EZREAL-A-DRAW-002")]
    public async Task CoreRuleEnginePlaysEzrealDiscardDrawTwo(
        string fixtureFileName,
        string sourceObjectId,
        string discardObjectId,
        string firstDrawObjectId,
        string secondDrawObjectId)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal([sourceObjectId], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([firstDrawObjectId, secondDrawObjectId], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal([discardObjectId], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, result.FinalState.CardObjects[sourceObjectId].Power);
    }

    [Theory]
    [InlineData("SFD·149/221", "P1-UNIT-EZREAL")]
    [InlineData("SFD·149a/221", "P1-UNIT-EZREAL-A")]
    public async Task CoreRuleEngineRejectsEzrealWhenDiscardTargetIsSource(string cardNo, string sourceObjectId)
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId]
                }
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-ezreal-source-discard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                sourceObjectId,
                cardNo,
                [sourceObjectId]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal([sourceObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSolariLeaderStunEnemyUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-solari-leader-stun-enemy-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SOLARI-LEADER"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BASE-SOLARI-LEADER-TARGET-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-SOLARI-LEADER"].Power);
        Assert.Contains(
            "STUNNED",
            result.FinalState.CardObjects["P2-BASE-SOLARI-LEADER-TARGET-001"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSolariLeaderDestroyAlreadyStunnedEnemyUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-solari-leader-destroy-stunned-enemy.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SOLARI-LEADER"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BASE-SOLARI-LEADER-STUNNED-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.False(result.FinalState.CardObjects.ContainsKey("P2-BASE-SOLARI-LEADER-STUNNED-001"));
        Assert.Contains("UNIT_DESTROYED", result.EventKinds);
        Assert.DoesNotContain("STATUS_EFFECT_APPLIED", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSolariLeaderWhenTargetIsFriendlyUnit()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-SOLARI-LEADER"],
                    Base = ["P1-BASE-SOLARI-LEADER-FRIENDLY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-SOLARI-LEADER-FRIENDLY-001"] = new(
                    "P1-BASE-SOLARI-LEADER-FRIENDLY-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-solari-leader-friendly-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SOLARI-LEADER",
                "OGN·225/298",
                ["P1-BASE-SOLARI-LEADER-FRIENDLY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-SOLARI-LEADER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SOLARI-LEADER-FRIENDLY-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBuhruCaptainDrawMode()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-buhru-captain-draw-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-BUHRU-CAPTAIN"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BUHRU-CAPTAIN-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-BUHRU-CAPTAIN"].Power);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBuhruCaptainSelfBoonMode()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-buhru-captain-self-boon-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-BUHRU-CAPTAIN"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-BUHRU-CAPTAIN"].Power);
        Assert.Contains(CardObjectTags.Boon, result.FinalState.CardObjects["P1-UNIT-BUHRU-CAPTAIN"].Tags);
    }

    [Fact]
    public void CoreRuleEngineBuhruCaptainPromptExposesModalChoices()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-BUHRU-CAPTAIN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-BUHRU-CAPTAIN"] = new(
                    "P1-UNIT-BUHRU-CAPTAIN",
                    cardNo: "SFD·091/221",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));

        Assert.Equal(["P1-UNIT-BUHRU-CAPTAIN"], (playCandidate.Sources ?? []).Select(source => source.Id).ToArray());
        Assert.Equal(["DRAW_1", "SELF_BOON"], (playCandidate.Modes ?? []).Select(mode => mode.Id).ToArray());
        Assert.Equal(["抽 1 张", "给予我增益"], (playCandidate.Modes ?? []).Select(mode => mode.Label).ToArray());
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBuhruCaptainWhenModeIsMissing()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-BUHRU-CAPTAIN"]
                }
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-buhru-captain-missing-mode", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-BUHRU-CAPTAIN",
                "SFD·091/221",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-BUHRU-CAPTAIN"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysChempunkToughDiscardHand()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-chempunk-tough-discard-hand.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-CHEMPUNK-TOUGH"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-CHEMPUNK-DISCARD-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-CHEMPUNK-TOUGH"].Power);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsChempunkToughWhenDiscardTargetIsSource()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-CHEMPUNK-TOUGH"]
                }
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-chempunk-tough-source-discard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CHEMPUNK-TOUGH",
                "OGN·003/298",
                ["P1-UNIT-CHEMPUNK-TOUGH"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-CHEMPUNK-TOUGH"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Theory]
    [InlineData("p2-preflight-play-jinx-discard-two-hand.fixture.json", "P1-UNIT-JINX", "P1-JINX-DISCARD-001", "P1-JINX-DISCARD-002")]
    [InlineData("p2-preflight-play-jinx-alt-a-discard-two-hand.fixture.json", "P1-UNIT-JINX-A", "P1-JINX-A-DISCARD-001", "P1-JINX-A-DISCARD-002")]
    public async Task CoreRuleEnginePlaysJinxDiscardTwoHand(
        string fixtureFileName,
        string sourceObjectId,
        string firstDiscardObjectId,
        string secondDiscardObjectId)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal([sourceObjectId], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal([firstDiscardObjectId, secondDiscardObjectId], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(4, result.FinalState.CardObjects[sourceObjectId].Power);
        Assert.Equal(2, result.EventKinds.Count(kind => string.Equals(kind, "CARD_DISCARDED", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("OGN·202/298", "P1-OGN-JINX-DISCARD-TRIGGER")]
    [InlineData("OGN·202a/298", "P1-OGN-JINX-A-DISCARD-TRIGGER")]
    [InlineData("ARC-005/006", "P1-ARC-JINX-DISCARD-TRIGGER")]
    public async Task P79JinxDiscardTriggerReadiesAndGainsPowerOnceForDiscardBatch(
        string triggerCardNo,
        string triggerSourceObjectId)
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand =
                    [
                        "P1-JINX-DISCARDER",
                        "P1-JINX-DISCARD-001",
                        "P1-JINX-DISCARD-002"
                    ],
                    Base =
                    [
                        triggerSourceObjectId,
                        "P1-JINX-DISCARD-TRIGGER-FACEDOWN",
                        "P1-JINX-DISCARD-TRIGGER-STANDBY"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-JINX-DISCARD-TRIGGER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-JINX-DISCARDER"] = new(
                    "P1-JINX-DISCARDER",
                    cardNo: "OGN·030/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "急速", "强攻2"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-JINX-DISCARD-001"] = new(
                    "P1-JINX-DISCARD-001",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-JINX-DISCARD-002"] = new(
                    "P1-JINX-DISCARD-002",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                [triggerSourceObjectId] = new(
                    triggerSourceObjectId,
                    cardNo: triggerCardNo,
                    isExhausted: true,
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-JINX-DISCARD-TRIGGER-FACEDOWN"] = new(
                    "P1-JINX-DISCARD-TRIGGER-FACEDOWN",
                    cardNo: triggerCardNo,
                    isFaceDown: true,
                    isExhausted: true,
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-JINX-DISCARD-TRIGGER-STANDBY"] = new(
                    "P1-JINX-DISCARD-TRIGGER-STANDBY",
                    isFaceDown: true,
                    cardNo: triggerCardNo,
                    isExhausted: true,
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-JINX-DISCARD-TRIGGER"] = new(
                    "P2-JINX-DISCARD-TRIGGER",
                    cardNo: triggerCardNo,
                    isExhausted: true,
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "OGN·030/298");

        var engine = new CoreRuleEngine();
        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-jinx-discard-trigger-play-discarder", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-JINX-DISCARDER",
                "OGN·030/298",
                ["P1-JINX-DISCARD-001", "P1-JINX-DISCARD-002"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-p7-9-jinx-discard-trigger-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-jinx-discard-trigger-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains("DISCARDED_HAND_CARD_THIS_TURN:P1", p2Pass.State.UntilEndOfTurnEffects);
        Assert.Equal(2, p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal)));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => gameEvent.Kind == "TRIGGER_RESOLVED"
            && Equals(gameEvent.Payload.GetValueOrDefault("sourceObjectId"), triggerSourceObjectId));
        Assert.True(p2Pass.State.CardObjects[triggerSourceObjectId].IsExhausted);
        Assert.Equal(5, p2Pass.State.CardObjects[triggerSourceObjectId].Power);
        var pendingTrigger = Assert.Single(p2Pass.State.StackItems);
        Assert.Equal(triggerSourceObjectId, pendingTrigger.SourceObjectId);
        Assert.Equal(2, pendingTrigger.HandDiscardObserver!.DiscardedCards.Count);
        // CN 383.3 / 423.1.b: the ordinary trigger gets its own response window.
        var triggerFirstPass = await engine.ResolveAsync(p2Pass.State,
            new PlayerIntent("intent-p7-9-jinx-trigger-first-pass", p2Pass.State.PriorityPlayerId!, "PASS_PRIORITY"),
            new PassPriorityCommand(), CancellationToken.None);
        Assert.True(triggerFirstPass.Accepted, triggerFirstPass.ErrorMessage);
        var triggerResolved = await engine.ResolveAsync(triggerFirstPass.State,
            new PlayerIntent("intent-p7-9-jinx-trigger-second-pass", triggerFirstPass.State.PriorityPlayerId!, "PASS_PRIORITY"),
            new PassPriorityCommand(), CancellationToken.None);
        Assert.True(triggerResolved.Accepted, triggerResolved.ErrorMessage);
        var triggerEvent = Assert.Single(triggerResolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "JINX_DISCARDED_HAND_CARDS_READY_POWER_1", StringComparison.Ordinal));
        Assert.Equal("P1", triggerEvent.Payload["playerId"]);
        Assert.Equal(triggerSourceObjectId, triggerEvent.Payload["sourceObjectId"]);
        Assert.Equal(["P1-JINX-DISCARD-001", "P1-JINX-DISCARD-002"], Assert.IsAssignableFrom<IReadOnlyList<string>>(triggerEvent.Payload["discardedObjectIds"]));
        Assert.Equal(2, triggerEvent.Payload["discardedCount"]);

        var readyEvent = Assert.Single(triggerResolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, triggerSourceObjectId, StringComparison.Ordinal));
        Assert.True((bool)readyEvent.Payload["wasExhausted"]!);
        Assert.False((bool)readyEvent.Payload["isExhausted"]!);

        var powerEvent = Assert.Single(triggerResolved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, triggerSourceObjectId, StringComparison.Ordinal));
        Assert.Equal(1, powerEvent.Payload["powerDelta"]);
        Assert.Equal(6, powerEvent.Payload["resultingPower"]);
        Assert.False(triggerResolved.State.CardObjects[triggerSourceObjectId].IsExhausted);
        Assert.Equal(6, triggerResolved.State.CardObjects[triggerSourceObjectId].Power);
        Assert.Equal(1, triggerResolved.State.CardObjects[triggerSourceObjectId].UntilEndOfTurnPowerModifier);
        Assert.True(triggerResolved.State.CardObjects["P1-JINX-DISCARD-TRIGGER-FACEDOWN"].IsExhausted);
        Assert.Equal(5, triggerResolved.State.CardObjects["P1-JINX-DISCARD-TRIGGER-FACEDOWN"].Power);
        Assert.True(triggerResolved.State.CardObjects["P1-JINX-DISCARD-TRIGGER-STANDBY"].IsExhausted);
        Assert.Equal(5, triggerResolved.State.CardObjects["P1-JINX-DISCARD-TRIGGER-STANDBY"].Power);
        Assert.True(triggerResolved.State.CardObjects["P2-JINX-DISCARD-TRIGGER"].IsExhausted);
        Assert.Equal(5, triggerResolved.State.CardObjects["P2-JINX-DISCARD-TRIGGER"].Power);
    }

    [Theory]
    [InlineData("OGN·030/298", "P1-UNIT-JINX", "P1-JINX-DISCARD-001")]
    [InlineData("OGN·030a/298", "P1-UNIT-JINX-A", "P1-JINX-A-DISCARD-001")]
    public async Task CoreRuleEngineRejectsJinxWhenDiscardTargetIncludesSource(
        string cardNo,
        string sourceObjectId,
        string discardObjectId)
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId, discardObjectId]
                }
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-jinx-source-discard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                sourceObjectId,
                cardNo,
                [sourceObjectId, discardObjectId]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal([sourceObjectId, discardObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }
}
