using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task P7TypedPowerPaymentAcceptsMatchingTraitAndDebitsOnlyThatTrait()
    {
        var state = PunishmentState(mana: 1) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 2,
                        [RuneTrait.Blue] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BULLET-TIME"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BULLET-TIME-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BULLET-TIME-UNIT-001"] = new("P2-BULLET-TIME-UNIT-001", power: 5)
            }
        };
        state = SelectedBattlefieldFixture.WithExplicitFields(state);

        var observed = await InstructionPaymentOriginalP7Assertions.Execute(
            state,
            new PlayerIntent("intent-bullet-time-red-power", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [SelectedBattlefieldFixture.Field],
                OptionalCosts: ["SPEND_POWER:red:2"]));
        var result = observed.Final;

        Assert.True(result.Accepted);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        Assert.False(result.State.RunePools["P1"].PowerByTrait.ContainsKey(RuneTrait.Red));
        Assert.Equal(2, result.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(2, costEvent.Payload["power"]);
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(2, powerByTrait[RuneTrait.Red]);
    }

    [Fact]
    public async Task P7PlayCardPromptOffersSpendPowerAmountsByAvailableTraitPower()
    {
        var state = PunishmentState(mana: 1) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 2
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BULLET-TIME"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BULLET-TIME-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2-BULLET-TIME-UNIT-001", power: 5)
            }
        };
        state = SelectedBattlefieldFixture.WithExplicitFields(state);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .ToArray();

        Assert.DoesNotContain(optionalCostChoices, choice => choice.Id.StartsWith("SPEND_POWER:", StringComparison.Ordinal));
        var played = await new CoreRuleEngine().ResolveAsync(state,
            new("p7-trait-amount-current-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("P1-SPELL-BULLET-TIME", "OGN·268/298", [SelectedBattlefieldFixture.Field]), default);
        Assert.True(played.Accepted, played.ErrorMessage);
        var payment = await InstructionPowerPaymentTimingTests.ToPayment(played.State);
        // Retain the historical method and original amount/trait boundaries;
        // CN204.3.a exposes these choices at the resolving instruction.
        var amounts = payment.PendingRuleChoice!.Request.Options;
        Assert.Contains(amounts, choice => string.Equals(choice.Id, "SPEND_POWER:1", StringComparison.Ordinal));
        Assert.Contains(amounts, choice => string.Equals(choice.Id, "SPEND_POWER:2", StringComparison.Ordinal));
        Assert.Contains(amounts, choice => string.Equals(choice.Id, "SPEND_POWER:red:1", StringComparison.Ordinal));
        Assert.Contains(amounts, choice => string.Equals(choice.Id, "SPEND_POWER:red:2", StringComparison.Ordinal));
        Assert.DoesNotContain(amounts, choice => string.Equals(choice.Id, "SPEND_POWER:3", StringComparison.Ordinal));
        Assert.DoesNotContain(amounts, choice => string.Equals(choice.Id, "SPEND_POWER:red:3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P7TypedPowerPaymentRejectsWhenRequiredTraitIsMissing()
    {
        var state = PunishmentState(mana: 1) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Blue] = 3
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BULLET-TIME"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BULLET-TIME-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BULLET-TIME-UNIT-001"] = new("P2-BULLET-TIME-UNIT-001", power: 5)
            }
        };
        state = SelectedBattlefieldFixture.WithExplicitFields(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-bullet-time-missing-red-power", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [SelectedBattlefieldFixture.Field],
                OptionalCosts: ["SPEND_POWER:red:2"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(3, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        Assert.Equal(["P1-SPELL-BULLET-TIME"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
        var opened = await InstructionPaymentOriginalP7Assertions.Open(state,
            new("p7-missing-trait-current-timing", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("P1-SPELL-BULLET-TIME", "OGN·268/298", [SelectedBattlefieldFixture.Field], OptionalCosts: ["SPEND_POWER:red:2"]));
        Assert.DoesNotContain(opened.Window.PendingRuleChoice!.Request.Options, o => o.Id == "SPEND_POWER:red:2");
        var rejected = await InstructionPowerPaymentTimingTests.Pay(opened.Window, "SPEND_POWER:red:2");
        Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(opened.Window), MatchStateHasher.Hash(rejected.State));
        Assert.Equal(3, rejected.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        Assert.Equal(0, rejected.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
    }

    [Fact]
    public async Task P7PlayCardRecyclesRuneAsPaymentResourceAction()
    {
        const string runeObjectId = "P1-RUNE-RED-PAYMENT-001";
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var state = PunishmentState(mana: 1) with
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
                    Hand = ["P1-SPELL-BULLET-TIME"],
                    Base = [runeObjectId],
                    RuneDeck = ["P1-RUNE-BOTTOM-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BULLET-TIME-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                [runeObjectId] = new(
                    runeObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2-BULLET-TIME-UNIT-001", power: 5)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new("P1", "HAND"),
                [runeObjectId] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM-001"] = new("P1", "RUNE_DECK"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2", "BATTLEFIELD")
            }
        };
        state = SelectedBattlefieldFixture.WithExplicitFields(state);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]);
        Assert.DoesNotContain(optionalCostChoices, choice => choice.Id.StartsWith("SPEND_POWER:", StringComparison.Ordinal));
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, paymentResourceAction, StringComparison.Ordinal));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["paymentResourceChoices"]);
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, paymentResourceAction, StringComparison.Ordinal));

        var observed = await InstructionPaymentOriginalP7Assertions.Execute(
            state,
            new PlayerIntent("intent-bullet-time-recycle-rune-payment", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [SelectedBattlefieldFixture.Field],
                OptionalCosts: [paymentResourceAction, "SPEND_POWER:red:1"]));
        var result = observed.Final;

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(runeObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001", runeObjectId], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal("RUNE_DECK", result.State.ObjectLocations[runeObjectId].Zone);
        Assert.False(result.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.False(result.State.RunePools["P1"].PowerByTrait.ContainsKey(RuneTrait.Red));
        Assert.Equal(1, result.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        var recycledEvent = Assert.Single(observed.Declaration.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.Equal("PLAY_CARD", recycledEvent.Payload["paymentWindow"]);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["SPEND_POWER:red:1"], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        var resourceCost = InstructionPaymentOriginalP7Assertions.DeclarationCost(observed);
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(resourceCost.Payload["paymentResourceActions"]));
        Assert.Equal([runeObjectId], Assert.IsType<string[]>(resourceCost.Payload["recycledRuneObjectIds"]));
    }

    [Fact]
    public async Task P7PlayCardRecyclesLegacyOwnedRuneAsPaymentResourceAction()
    {
        const string runeObjectId = "P1-RUNE-RED-PAYMENT-LEGACY";
        const string dirtyRuneObjectId = "P1-RUNE-RED-PAYMENT-DIRTY-P2";
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var dirtyPaymentResourceAction = $"RECYCLE_RUNE:{dirtyRuneObjectId}";
        var state = PunishmentState(mana: 1) with
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
                    Hand = ["P1-SPELL-BULLET-TIME"],
                    Base = [dirtyRuneObjectId, runeObjectId],
                    RuneDeck = ["P1-RUNE-BOTTOM-LEGACY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BULLET-TIME-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                [dirtyRuneObjectId] = new(
                    dirtyRuneObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P2",
                    controllerId: ""),
                [runeObjectId] = new(
                    runeObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P1",
                    controllerId: ""),
                ["P1-RUNE-BOTTOM-LEGACY"] = new(
                    "P1-RUNE-BOTTOM-LEGACY",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2-BULLET-TIME-UNIT-001", power: 5)
            }
        };
        state = SelectedBattlefieldFixture.WithExplicitFields(state);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["paymentResourceChoices"]);
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, paymentResourceAction, StringComparison.Ordinal));
        Assert.DoesNotContain(paymentResourceChoices, choice => string.Equals(choice.Id, dirtyPaymentResourceAction, StringComparison.Ordinal));

        var observed = await InstructionPaymentOriginalP7Assertions.Execute(
            state,
            new PlayerIntent("intent-bullet-time-legacy-recycle-rune-payment", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [SelectedBattlefieldFixture.Field],
                OptionalCosts: [paymentResourceAction, "SPEND_POWER:red:1"]));
        var result = observed.Final;

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal([dirtyRuneObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-LEGACY", runeObjectId], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(1, result.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        var costEvent = InstructionPaymentOriginalP7Assertions.DeclarationCost(observed);
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([runeObjectId], Assert.IsType<string[]>(costEvent.Payload["recycledRuneObjectIds"]));
    }

    [Fact]
    public async Task P7PlayCardPromptOffersRecycleRuneForPartialSpendPowerAmount()
    {
        const string runeObjectId = "P1-RUNE-RED-PARTIAL-PAYMENT-001";
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var state = PunishmentState(mana: 1) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BULLET-TIME"],
                    Base = [runeObjectId],
                    RuneDeck = ["P1-RUNE-BOTTOM-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BULLET-TIME-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                [runeObjectId] = new(
                    runeObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2-BULLET-TIME-UNIT-001", power: 5)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new("P1", "HAND"),
                [runeObjectId] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM-001"] = new("P1", "RUNE_DECK"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2", "BATTLEFIELD")
            }
        };
        state = SelectedBattlefieldFixture.WithExplicitFields(state);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .ToArray();
        Assert.DoesNotContain(optionalCostChoices, choice => choice.Id.StartsWith("SPEND_POWER:", StringComparison.Ordinal));
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, paymentResourceAction, StringComparison.Ordinal));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["paymentResourceChoices"])
            .ToArray();
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, paymentResourceAction, StringComparison.Ordinal));
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["availablePower"]));
        Assert.Equal(2, Assert.IsType<int>(sourceRequirement["availablePowerWithPaymentResources"]));
        var availablePowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(sourceRequirement["availablePowerByTrait"]);
        Assert.Equal(1, availablePowerByTrait[RuneTrait.Red]);
        var availablePowerByTraitWithPaymentResources = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            sourceRequirement["availablePowerByTraitWithPaymentResources"]);
        Assert.Equal(2, availablePowerByTraitWithPaymentResources[RuneTrait.Red]);

        var observed = await InstructionPaymentOriginalP7Assertions.Execute(
            state,
            new PlayerIntent("intent-bullet-time-partial-recycle-rune-payment", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [SelectedBattlefieldFixture.Field],
                OptionalCosts: [paymentResourceAction, "SPEND_POWER:red:2"]));
        var result = observed.Final;

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(runeObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001", runeObjectId], result.State.PlayerZones["P1"].RuneDeck);
        Assert.False(result.State.RunePools["P1"].PowerByTrait.ContainsKey(RuneTrait.Red));
        Assert.Equal(2, result.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["SPEND_POWER:red:2"], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        var resourceCost = InstructionPaymentOriginalP7Assertions.DeclarationCost(observed);
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(resourceCost.Payload["paymentResourceActions"]));
        Assert.Equal([runeObjectId], Assert.IsType<string[]>(resourceCost.Payload["recycledRuneObjectIds"]));
    }

    [Fact]
    public async Task P7PlayCardPaymentResourceContributionMetadataSeparatesTraits()
    {
        const string redRuneObjectId = "P1-RUNE-RED-PARTIAL-PAYMENT-001";
        const string blueRuneObjectId = "P1-RUNE-BLUE-EXTRA-PAYMENT-001";
        var redPaymentResourceAction = $"RECYCLE_RUNE:{redRuneObjectId}";
        var bluePaymentResourceAction = $"RECYCLE_RUNE:{blueRuneObjectId}";
        var state = PunishmentState(mana: 1) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BULLET-TIME"],
                    Base = [redRuneObjectId, blueRuneObjectId],
                    RuneDeck = ["P1-RUNE-BOTTOM-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BULLET-TIME-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                [redRuneObjectId] = new(
                    redRuneObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P1",
                    controllerId: "P1"),
                [blueRuneObjectId] = new(
                    blueRuneObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2-BULLET-TIME-UNIT-001", power: 5)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new("P1", "HAND"),
                [redRuneObjectId] = new("P1", "BASE"),
                [blueRuneObjectId] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM-001"] = new("P1", "RUNE_DECK"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2", "BATTLEFIELD")
            }
        };
        state = SelectedBattlefieldFixture.WithExplicitFields(state);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]);
        Assert.Equal(RuneTrait.Red, Assert.IsType<string>(paymentResourcePowerByChoice[redPaymentResourceAction]["trait"]));
        Assert.Equal(1, Assert.IsType<int>(paymentResourcePowerByChoice[redPaymentResourceAction]["power"]));
        Assert.Equal(RuneTrait.Blue, Assert.IsType<string>(paymentResourcePowerByChoice[bluePaymentResourceAction]["trait"]));
        Assert.Equal(1, Assert.IsType<int>(paymentResourcePowerByChoice[bluePaymentResourceAction]["power"]));

        var rejected = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-bullet-time-mismatched-recycle-rune-payment", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [SelectedBattlefieldFixture.Field],
                OptionalCosts: [bluePaymentResourceAction, "SPEND_POWER:red:2"]),
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Equal([redRuneObjectId, blueRuneObjectId], rejected.State.PlayerZones["P1"].Base);
        Assert.Equal(1, rejected.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Empty(rejected.Events);
        var opened = await InstructionPaymentOriginalP7Assertions.Open(state,
            new("p7-mismatched-trait-current-timing", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("P1-SPELL-BULLET-TIME", "OGN·268/298", [SelectedBattlefieldFixture.Field],
                OptionalCosts: [bluePaymentResourceAction, "SPEND_POWER:red:2"]));
        Assert.DoesNotContain(opened.Window.PendingRuleChoice!.Request.Options, o => o.Id == "SPEND_POWER:red:2");
        var unpaid = await InstructionPowerPaymentTimingTests.Pay(opened.Window, "SPEND_POWER:red:2");
        Assert.False(unpaid.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, unpaid.ErrorCode); Assert.Empty(unpaid.Events);
        Assert.Equal(MatchStateHasher.Hash(opened.Window), MatchStateHasher.Hash(unpaid.State));
        Assert.Equal(1, unpaid.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, unpaid.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        Assert.Contains(blueRuneObjectId, unpaid.State.PlayerZones["P1"].RuneDeck);

        var observed = await InstructionPaymentOriginalP7Assertions.Execute(
            state,
            new PlayerIntent("intent-bullet-time-matching-recycle-rune-payment", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [SelectedBattlefieldFixture.Field],
                OptionalCosts: [redPaymentResourceAction, "SPEND_POWER:red:2"]));
        var accepted = observed.Final;

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.DoesNotContain(redRuneObjectId, accepted.State.PlayerZones["P1"].Base);
        Assert.Contains(blueRuneObjectId, accepted.State.PlayerZones["P1"].Base);
        Assert.Equal(2, accepted.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        var costEvent = InstructionPaymentOriginalP7Assertions.DeclarationCost(observed);
        Assert.Equal([redPaymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
    }

    [Fact]
    public async Task P7PlayCardGenericPaymentResourceCanUseMixedTraitContribution()
    {
        const string redRuneObjectId = "P1-RUNE-RED-PARTIAL-PAYMENT-001";
        const string blueRuneObjectId = "P1-RUNE-BLUE-EXTRA-PAYMENT-001";
        var redPaymentResourceAction = $"RECYCLE_RUNE:{redRuneObjectId}";
        var bluePaymentResourceAction = $"RECYCLE_RUNE:{blueRuneObjectId}";
        var state = PunishmentState(mana: 1) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BULLET-TIME"],
                    Base = [redRuneObjectId, blueRuneObjectId],
                    RuneDeck = ["P1-RUNE-BOTTOM-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BULLET-TIME-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                [redRuneObjectId] = new(
                    redRuneObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P1",
                    controllerId: "P1"),
                [blueRuneObjectId] = new(
                    blueRuneObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2-BULLET-TIME-UNIT-001", power: 5)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new("P1", "HAND"),
                [redRuneObjectId] = new("P1", "BASE"),
                [blueRuneObjectId] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM-001"] = new("P1", "RUNE_DECK"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2", "BATTLEFIELD")
            }
        };
        state = SelectedBattlefieldFixture.WithExplicitFields(state);

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
        Assert.DoesNotContain(optionalCostChoices, id => id.StartsWith("SPEND_POWER:", StringComparison.Ordinal));
        Assert.Contains(redPaymentResourceAction, optionalCostChoices);
        Assert.Contains(bluePaymentResourceAction, optionalCostChoices);
        Assert.Equal(1, Assert.IsType<int>(sourceRequirement["availablePower"]));
        Assert.Equal(3, Assert.IsType<int>(sourceRequirement["availablePowerWithPaymentResources"]));
        var availablePowerByTraitWithPaymentResources = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            sourceRequirement["availablePowerByTraitWithPaymentResources"]);
        Assert.Equal(2, availablePowerByTraitWithPaymentResources[RuneTrait.Red]);
        Assert.Equal(1, availablePowerByTraitWithPaymentResources[RuneTrait.Blue]);

        var observed = await InstructionPaymentOriginalP7Assertions.Execute(
            state,
            new PlayerIntent("intent-bullet-time-generic-mixed-recycle-rune-payment", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [SelectedBattlefieldFixture.Field],
                OptionalCosts: [bluePaymentResourceAction, "SPEND_POWER:2"]));
        var accepted = observed.Final;

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Contains(redRuneObjectId, accepted.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(blueRuneObjectId, accepted.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001", blueRuneObjectId], accepted.State.PlayerZones["P1"].RuneDeck);
        Assert.False(accepted.State.RunePools["P1"].PowerByTrait.ContainsKey(RuneTrait.Red));
        Assert.False(accepted.State.RunePools["P1"].PowerByTrait.ContainsKey(RuneTrait.Blue));
        Assert.Equal(2, accepted.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        var costEvent = Assert.Single(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["SPEND_POWER:2"], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        var resourceCost = InstructionPaymentOriginalP7Assertions.DeclarationCost(observed);
        Assert.Equal([bluePaymentResourceAction], Assert.IsType<string[]>(resourceCost.Payload["paymentResourceActions"]));
        Assert.Equal([blueRuneObjectId], Assert.IsType<string[]>(resourceCost.Payload["recycledRuneObjectIds"]));
    }

    [Fact]
    public async Task P7PlayCardAllowsRequiredMultipleRecycledPaymentResourceActions()
    {
        const string firstRuneObjectId = "P1-RUNE-RED-PARTIAL-PAYMENT-001";
        const string secondRuneObjectId = "P1-RUNE-RED-EXTRA-PAYMENT-001";
        var firstPaymentResourceAction = $"RECYCLE_RUNE:{firstRuneObjectId}";
        var secondPaymentResourceAction = $"RECYCLE_RUNE:{secondRuneObjectId}";
        var state = PunishmentState(mana: 1) with
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
                    Hand = ["P1-SPELL-BULLET-TIME"],
                    Base = [firstRuneObjectId, secondRuneObjectId],
                    RuneDeck = ["P1-RUNE-BOTTOM-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BULLET-TIME-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                [firstRuneObjectId] = new(
                    firstRuneObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P1",
                    controllerId: "P1"),
                [secondRuneObjectId] = new(
                    secondRuneObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2-BULLET-TIME-UNIT-001", power: 5)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new("P1", "HAND"),
                [firstRuneObjectId] = new("P1", "BASE"),
                [secondRuneObjectId] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM-001"] = new("P1", "RUNE_DECK"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2", "BATTLEFIELD")
            }
        };
        state = SelectedBattlefieldFixture.WithExplicitFields(state);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["paymentResourceChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Contains(firstPaymentResourceAction, paymentResourceChoices);
        Assert.Contains(secondPaymentResourceAction, paymentResourceChoices);
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["availablePower"]));
        Assert.Equal(2, Assert.IsType<int>(sourceRequirement["availablePowerWithPaymentResources"]));

        var observed = await InstructionPaymentOriginalP7Assertions.Execute(
            state,
            new PlayerIntent("intent-bullet-time-double-recycle-rune-payment", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [SelectedBattlefieldFixture.Field],
                OptionalCosts: [firstPaymentResourceAction, secondPaymentResourceAction, "SPEND_POWER:red:2"]));
        var result = observed.Final;

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001", firstRuneObjectId, secondRuneObjectId], result.State.PlayerZones["P1"].RuneDeck);
        Assert.False(result.State.RunePools["P1"].PowerByTrait.ContainsKey(RuneTrait.Red));
        Assert.Equal(2, result.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        Assert.Equal(2, observed.Declaration.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal)));
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["SPEND_POWER:red:2"], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        var resourceCost = InstructionPaymentOriginalP7Assertions.DeclarationCost(observed);
        Assert.Equal(
            [firstPaymentResourceAction, secondPaymentResourceAction],
            Assert.IsType<string[]>(resourceCost.Payload["paymentResourceActions"]));
        Assert.Equal([firstRuneObjectId, secondRuneObjectId], Assert.IsType<string[]>(resourceCost.Payload["recycledRuneObjectIds"]));
    }

    [Fact]
    public async Task P7PlayCardRejectsOverRecycledPaymentResourceActions()
    {
        const string firstRuneObjectId = "P1-RUNE-RED-PARTIAL-PAYMENT-001";
        const string secondRuneObjectId = "P1-RUNE-RED-EXTRA-PAYMENT-001";
        var firstPaymentResourceAction = $"RECYCLE_RUNE:{firstRuneObjectId}";
        var secondPaymentResourceAction = $"RECYCLE_RUNE:{secondRuneObjectId}";
        var state = PunishmentState(mana: 1) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BULLET-TIME"],
                    Base = [firstRuneObjectId, secondRuneObjectId],
                    RuneDeck = ["P1-RUNE-BOTTOM-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BULLET-TIME-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                [firstRuneObjectId] = new(
                    firstRuneObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P1",
                    controllerId: "P1"),
                [secondRuneObjectId] = new(
                    secondRuneObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2-BULLET-TIME-UNIT-001", power: 5)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new("P1", "HAND"),
                [firstRuneObjectId] = new("P1", "BASE"),
                [secondRuneObjectId] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM-001"] = new("P1", "RUNE_DECK"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2", "BATTLEFIELD")
            }
        };
        state = SelectedBattlefieldFixture.WithExplicitFields(state);
        var initialStateHash = MatchStateHasher.Hash(state);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]);
        Assert.Equal(RuneTrait.Red, Assert.IsType<string>(paymentResourcePowerByChoice[firstPaymentResourceAction]["trait"]));
        Assert.Equal(1, Assert.IsType<int>(paymentResourcePowerByChoice[firstPaymentResourceAction]["power"]));
        Assert.Equal(RuneTrait.Red, Assert.IsType<string>(paymentResourcePowerByChoice[secondPaymentResourceAction]["trait"]));
        Assert.Equal(1, Assert.IsType<int>(paymentResourcePowerByChoice[secondPaymentResourceAction]["power"]));

        var observed = await InstructionPaymentOriginalP7Assertions.Execute(
            state,
            new PlayerIntent("intent-bullet-time-over-recycle-rune-payment", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [SelectedBattlefieldFixture.Field],
                OptionalCosts: [firstPaymentResourceAction, secondPaymentResourceAction, "SPEND_POWER:red:2"]));
        var result = observed.Final;

        // Retain the historical method identity and original failure. CN 164.2.b,
        // 166-167, 357.1.a and 430 allow acquiring more power than the actual cost.
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(initialStateHash, MatchStateHasher.Hash(state));
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001", firstRuneObjectId, secondRuneObjectId], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(2, result.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        var paid = Assert.Single(result.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.Equal(2, paid.Payload["totalPowerCost"]);
        var resourceCost = InstructionPaymentOriginalP7Assertions.DeclarationCost(observed);
        Assert.Equal([firstPaymentResourceAction, secondPaymentResourceAction], Assert.IsType<string[]>(resourceCost.Payload["paymentResourceActions"]));
        Assert.Equal([firstRuneObjectId, secondRuneObjectId], observed.Declaration.Events.Where(gameEvent => gameEvent.Kind == "RUNE_RECYCLED")
            .Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["sourceObjectId"])));
    }

    [Fact]
    public async Task P7PlayCardRecyclesRuneForHasteReadyPaymentResourceAction()
    {
        const string runeObjectId = "P1-RUNE-PURPLE-HASTE-PAYMENT-001";
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var state = PunishmentState(mana: 5) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(5, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-SIVIR"],
                    Base = [runeObjectId],
                    RuneDeck = ["P1-RUNE-BOTTOM-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-SIVIR"] = new(
                    "P1-UNIT-SIVIR",
                    cardNo: "SFD·143/221",
                    ownerId: "P1",
                    controllerId: "P1"),
                [runeObjectId] = new(
                    runeObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:purple"],
                    cardNo: "UNL-R04",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-SIVIR"] = new("P1", "HAND"),
                [runeObjectId] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM-001"] = new("P1", "RUNE_DECK")
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "SFD·143/221");

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]);
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, HasteOptionalCostNames.HasteReady, StringComparison.Ordinal));
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, paymentResourceAction, StringComparison.Ordinal));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["paymentResourceChoices"]);
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, paymentResourceAction, StringComparison.Ordinal));

        var engine = new CoreRuleEngine();
        var playResult = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-sivir-recycle-rune-haste-ready-payment", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SIVIR",
                "SFD·143/221",
                [],
                OptionalCosts: [paymentResourceAction, HasteOptionalCostNames.HasteReady]),
            CancellationToken.None);

        Assert.True(playResult.Accepted, playResult.ErrorMessage);
        Assert.DoesNotContain(runeObjectId, playResult.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001", runeObjectId], playResult.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal("RUNE_DECK", playResult.State.ObjectLocations[runeObjectId].Zone);
        Assert.False(playResult.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal(0, playResult.State.RunePools["P1"].Mana);
        Assert.False(playResult.State.RunePools["P1"].PowerByTrait.ContainsKey(RuneTrait.Purple));
        var recycledEvent = Assert.Single(playResult.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        Assert.Equal("PLAY_CARD", recycledEvent.Payload["paymentWindow"]);
        var costEvent = Assert.Single(playResult.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.StartsWith("PLAY_CARD:", Assert.IsType<string>(costEvent.Payload["paymentId"]), StringComparison.Ordinal);
        Assert.Equal("PLAY_CARD", costEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-UNIT-SIVIR", costEvent.Payload["sourceObjectId"]);
        Assert.Equal("SIVIR_PLAY_UNIT_NO_OPTIONAL_HASTE", costEvent.Payload["reason"]);
        Assert.Equal(5, costEvent.Payload["mana"]);
        Assert.Equal(2, costEvent.Payload["power"]);
        Assert.Equal(4, costEvent.Payload["baseManaCost"]);
        Assert.Equal(5, costEvent.Payload["totalManaCost"]);
        Assert.Equal(0, costEvent.Payload["genericPower"]);
        Assert.Equal(2, costEvent.Payload["totalPowerCost"]);
        Assert.Equal([HasteOptionalCostNames.HasteReady], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([runeObjectId], Assert.IsType<string[]>(costEvent.Payload["recycledRuneObjectIds"]));
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(2, powerByTrait[RuneTrait.Purple]);
        var remainingPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["remainingPowerByTrait"]);
        Assert.DoesNotContain(RuneTrait.Purple, remainingPowerByTrait.Keys);

        // CN 359.2: entry has already completed in PLAY_CARD.
        var p2Pass = playResult;

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-UNIT-SIVIR"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.False(p2Pass.State.CardObjects["P1-UNIT-SIVIR"].IsExhausted);
        var unitPlayedEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
        Assert.Equal(true, unitPlayedEvent.Payload["hasteReadyOptionalCostPaid"]);
        Assert.Equal(false, unitPlayedEvent.Payload["isExhausted"]);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsTapRuneSourceWithoutCardNo()
    {
        const string runeObjectId = "P1-RUNE-UNKNOWN-TAP";
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [runeObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new(
                    runeObjectId,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-basic-rune-tap-unknown", "P1", "TAP_RUNE"),
            new TapRuneCommand(runeObjectId),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal("横置符文需要服务端已确认的符文牌信息。", result.ErrorMessage);
        Assert.DoesNotContain("TAP_RUNE", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal([runeObjectId], result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
    }

    [Fact]
    public async Task CoreRuleEngineTapsBasicRuneAndReconcilesObjectLocation()
    {
        const string runeObjectId = "P1-RUNE-RED-TAP";
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [runeObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new(
                    runeObjectId,
                    cardNo: "UNL-R01",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-basic-rune-tap-location", "P1", "TAP_RUNE"),
            new TapRuneCommand(runeObjectId),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.True(result.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("BASE", result.State.ObjectLocations[runeObjectId].Zone);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsAcceptedTapRuneReplayWithoutMutation()
    {
        const string runeObjectId = "P1-RUNE-RED-TAP-REPLAY";
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [runeObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new(
                    runeObjectId,
                    cardNo: "UNL-R01",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var command = new TapRuneCommand(runeObjectId);

        var accepted = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-basic-rune-tap-before-replay", "P1", CommandTypes.TapRune),
            command,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(["RUNE_TAPPED", "MANA_GAINED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(1, accepted.State.RunePools["P1"].Mana);
        Assert.True(accepted.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("BASE", accepted.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(accepted.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertTapRuneOrdinaryMainPromptQueueAudit(accepted, runeObjectId);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await engine.ResolveAsync(
            accepted.State,
            new PlayerIntent("intent-basic-rune-tap-stale-replay", "P1", CommandTypes.TapRune),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(1, replay.State.RunePools["P1"].Mana);
        Assert.True(replay.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("BASE", replay.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(replay.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertTapRuneOrdinaryMainPromptQueueAudit(replay, runeObjectId);
    }

    [Fact]
    public async Task TapRuneStalePromptReplayAfterRuneExhaustsRejectsWithoutMutation()
    {
        const string runeObjectId = "P1-RUNE-RED-TAP-STALE-PROMPT";
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [runeObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new(
                    runeObjectId,
                    cardNo: "UNL-R01",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new TapRuneCommand(runeObjectId);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.TapRune, prompt.EnabledActions());
        Assert.Contains(prompt.Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.TapRune, prompt);
        const string acceptedClientIntentId = "intent-basic-rune-tap-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-basic-rune-tap-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            NativeCommandWire.Bind(command, ref staleRawCommand),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["RUNE_TAPPED", "MANA_GAINED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(1, accepted.State.RunePools["P1"].Mana);
        Assert.True(accepted.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("BASE", accepted.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(accepted.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertTapRuneOrdinaryMainPromptQueueAudit(accepted, runeObjectId);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var currentPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var currentSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.TapRune, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.TapRune, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            NativeCommandWire.Bind(command, ref staleRawCommand),
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(1, replay.State.RunePools["P1"].Mana);
        Assert.True(replay.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("BASE", replay.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(replay.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertTapRuneOrdinaryMainPromptQueueAudit(replay, runeObjectId);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = Assert.Single(journal.Entries, entry => !entry.Accepted);
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.TapRune, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.TapRune, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, rejectedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var duplicateRejected = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            NativeCommandWire.Bind(command, ref staleRawCommand),
            CancellationToken.None);

        Assert.False(duplicateRejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateRejected.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateRejected.ErrorMessage);
        Assert.Empty(duplicateRejected.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicateRejected.State));
        Assert.Equal(replay.State.Tick, duplicateRejected.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(duplicateRejected.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(duplicateRejected.Snapshots));
        Assert.Equal(1, duplicateRejected.State.RunePools["P1"].Mana);
        Assert.True(duplicateRejected.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("BASE", duplicateRejected.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(duplicateRejected.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertTapRuneOrdinaryMainPromptQueueAudit(duplicateRejected, runeObjectId);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.TapRune,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        var conflict = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            NativeCommandWire.Bind(command, ref changedRawCommand),
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(1, conflict.State.RunePools["P1"].Mana);
        Assert.True(conflict.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("BASE", conflict.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(conflict.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertTapRuneOrdinaryMainPromptQueueAudit(conflict, runeObjectId);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TapRuneDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string runeObjectId = "P1-RUNE-RED-TAP-RAW-IDEMPOTENCY";
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [runeObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new(
                    runeObjectId,
                    cardNo: "UNL-R01",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new TapRuneCommand(runeObjectId);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.TapRune, prompt.EnabledActions());
        Assert.Contains(prompt.Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        var rawCommand = PromptScopedRawCommand(CommandTypes.TapRune, prompt);
        const string clientIntentId = "intent-basic-rune-tap-raw-idempotency";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref rawCommand),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["RUNE_TAPPED", "MANA_GAINED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(1, accepted.State.RunePools["P1"].Mana);
        Assert.True(accepted.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("BASE", accepted.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(accepted.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertTapRuneOrdinaryMainPromptQueueAudit(accepted, runeObjectId);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.TapRune, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.TapRune, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(journalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref rawCommand),
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Null(replay.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(["RUNE_TAPPED", "MANA_GAINED"], replay.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(1, replay.State.RunePools["P1"].Mana);
        Assert.True(replay.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("BASE", replay.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(replay.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertTapRuneOrdinaryMainPromptQueueAudit(replay, runeObjectId);
        Assert.Single(journal.Entries);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.TapRune,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref changedRawCommand),
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(1, conflict.State.RunePools["P1"].Mana);
        Assert.True(conflict.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("BASE", conflict.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(conflict.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertTapRuneOrdinaryMainPromptQueueAudit(conflict, runeObjectId);
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    private static void AssertTapRuneOrdinaryMainPromptQueueAudit(ResolutionResult result, string runeObjectId)
    {
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.True(result.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("P1", result.State.ObjectLocations[runeObjectId].PlayerId);
        Assert.Equal("BASE", result.State.ObjectLocations[runeObjectId].Zone);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal("P1", snapshot.ActivePlayerId);
            Assert.Equal("P1", Assert.IsType<string>(snapshot.Timing["turnPlayerId"]));
            Assert.Equal(MatchPhases.Main, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Null(snapshot.Timing["priorityPlayerId"]);
            Assert.Null(snapshot.Timing["focusPlayerId"]);
            Assert.Empty(snapshot.Stack);

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }

        Assert.Equal("P1", result.Prompts["P1"].PlayerId);
        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal(PromptTypes.MainAction, result.Prompts["P1"].View?.Type);
        Assert.Equal(result.State.Tick, result.Prompts["P1"].SnapshotTick);
        Assert.Contains(CommandTypes.EndTurn, result.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(result.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.TapRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));

        Assert.Equal("P2", result.Prompts["P2"].PlayerId);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.DoesNotContain(CommandTypes.TapRune, result.Prompts["P2"].EnabledActions());
        Assert.Equal(result.State.Tick, result.Prompts["P2"].SnapshotTick);
    }

    [Fact]
    public async Task CoreRuleEnginePromptsAndTapsLegacyOwnedBasicRune()
    {
        const string runeObjectId = "P1-RUNE-RED-TAP-LEGACY";
        const string dirtyRuneObjectId = "P1-RUNE-RED-TAP-DIRTY-P2";
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [dirtyRuneObjectId, runeObjectId],
                    RuneDeck = ["P1-RUNE-BOTTOM-TAP-LEGACY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [dirtyRuneObjectId] = new(
                    dirtyRuneObjectId,
                    cardNo: "UNL-R01",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P2",
                    controllerId: ""),
                [runeObjectId] = new(
                    runeObjectId,
                    cardNo: "UNL-R01",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: ""),
                ["P1-RUNE-BOTTOM-TAP-LEGACY"] = new(
                    "P1-RUNE-BOTTOM-TAP-LEGACY",
                    cardNo: "UNL-R03",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var tapCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "TAP_RUNE", StringComparison.Ordinal));
        Assert.Contains(tapCandidate.Sources ?? [], source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(tapCandidate.Sources ?? [], source => string.Equals(source.Id, dirtyRuneObjectId, StringComparison.Ordinal));
        var recycleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "RECYCLE_RUNE", StringComparison.Ordinal));
        Assert.Contains(recycleCandidate.Sources ?? [], source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(recycleCandidate.Sources ?? [], source => string.Equals(source.Id, dirtyRuneObjectId, StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-basic-rune-tap-legacy", "P1", "TAP_RUNE"),
            new TapRuneCommand(runeObjectId),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.True(result.State.CardObjects[runeObjectId].IsExhausted);
        Assert.False(result.State.CardObjects[dirtyRuneObjectId].IsExhausted);
        Assert.Equal("BASE", result.State.ObjectLocations[runeObjectId].Zone);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsRecycleRuneSourceWithoutCardNo()
    {
        const string runeObjectId = "P1-RUNE-UNKNOWN-RECYCLE";
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [runeObjectId],
                    RuneDeck = ["P1-RUNE-BOTTOM-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new(
                    runeObjectId,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    cardNo: "UNL-R03",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-basic-rune-recycle-unknown", "P1", "RECYCLE_RUNE"),
            new RecycleRuneCommand(runeObjectId),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal("回收符文需要服务端已确认的符文牌信息。", result.ErrorMessage);
        Assert.DoesNotContain("RECYCLE_RUNE", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal([runeObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001"], result.State.PlayerZones["P1"].RuneDeck);
        Assert.False(result.State.RunePools["P1"].PowerByTrait.ContainsKey(RuneTrait.Red));
    }

    [Fact]
    public async Task CoreRuleEngineRecyclesBasicRuneForMatchingTraitPower()
    {
        const string runeObjectId = "P1-RUNE-RED-001";
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [runeObjectId],
                    RuneDeck = ["P1-RUNE-BOTTOM-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new(
                    runeObjectId,
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM-001"] = new("P1", "RUNE_DECK")
            }
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var recycleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "RECYCLE_RUNE", StringComparison.Ordinal));
        Assert.True(recycleCandidate.Enabled);
        Assert.Contains(recycleCandidate.Sources ?? [], source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-basic-rune-recycle-red", "P1", "RECYCLE_RUNE"),
            new RecycleRuneCommand(runeObjectId),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.DoesNotContain(runeObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001", runeObjectId], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, result.State.RunePools["P1"].TotalPower);
        Assert.False(result.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("RUNE_DECK", result.State.ObjectLocations[runeObjectId].Zone);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trait"] as string, RuneTrait.Red, StringComparison.Ordinal));
        var powerEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(RuneTrait.Red, powerEvent.Payload["trait"]);
        Assert.Equal(1, powerEvent.Payload["power"]);
        Assert.Equal(1, powerEvent.Payload["traitPowerAfter"]);

        var postPrompt = result.Prompts["P1"];
        Assert.DoesNotContain(postPrompt.EnabledActions(), action => string.Equals(action, "RECYCLE_RUNE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsAcceptedRecycleRuneReplayWithoutMutation()
    {
        const string runeObjectId = "P1-RUNE-RED-RECYCLE-REPLAY";
        const string bottomRuneObjectId = "P1-RUNE-BOTTOM-RECYCLE-REPLAY";
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [runeObjectId],
                    RuneDeck = [bottomRuneObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new(
                    runeObjectId,
                    isExhausted: true,
                    cardNo: "UNL-R01",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: "P1"),
                [bottomRuneObjectId] = new(
                    bottomRuneObjectId,
                    cardNo: "UNL-R03",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new("P1", "BASE"),
                [bottomRuneObjectId] = new("P1", "RUNE_DECK")
            }
        };
        var command = new RecycleRuneCommand(runeObjectId);

        var accepted = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-basic-rune-recycle-before-replay", "P1", CommandTypes.RecycleRune),
            command,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(["RUNE_RECYCLED", "POWER_GAINED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Empty(accepted.State.PlayerZones["P1"].Base);
        Assert.Equal([bottomRuneObjectId, runeObjectId], accepted.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(1, accepted.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, accepted.State.RunePools["P1"].TotalPower);
        Assert.False(accepted.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("RUNE_DECK", accepted.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(accepted.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.RecycleRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertRecycleRuneOrdinaryMainPromptQueueAudit(accepted, runeObjectId, bottomRuneObjectId);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await engine.ResolveAsync(
            accepted.State,
            new PlayerIntent("intent-basic-rune-recycle-stale-replay", "P1", CommandTypes.RecycleRune),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Empty(replay.State.PlayerZones["P1"].Base);
        Assert.Equal([bottomRuneObjectId, runeObjectId], replay.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(1, replay.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, replay.State.RunePools["P1"].TotalPower);
        Assert.False(replay.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("RUNE_DECK", replay.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(replay.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.RecycleRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertRecycleRuneOrdinaryMainPromptQueueAudit(replay, runeObjectId, bottomRuneObjectId);
    }

    [Fact]
    public async Task RecycleRuneStalePromptReplayAfterRuneMovesToRuneDeckRejectsWithoutMutation()
    {
        const string runeObjectId = "P1-RUNE-RED-RECYCLE-STALE-PROMPT";
        const string bottomRuneObjectId = "P1-RUNE-BOTTOM-RECYCLE-STALE-PROMPT";
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [runeObjectId],
                    RuneDeck = [bottomRuneObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new(
                    runeObjectId,
                    isExhausted: true,
                    cardNo: "UNL-R01",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: "P1"),
                [bottomRuneObjectId] = new(
                    bottomRuneObjectId,
                    cardNo: "UNL-R03",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new("P1", "BASE"),
                [bottomRuneObjectId] = new("P1", "RUNE_DECK")
            }
        };
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new RecycleRuneCommand(runeObjectId);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.RecycleRune, prompt.EnabledActions());
        Assert.Contains(prompt.Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.RecycleRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.RecycleRune, prompt);
        const string acceptedClientIntentId = "intent-basic-rune-recycle-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-basic-rune-recycle-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            NativeCommandWire.Bind(command, ref staleRawCommand),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["RUNE_RECYCLED", "POWER_GAINED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Empty(accepted.State.PlayerZones["P1"].Base);
        Assert.Equal([bottomRuneObjectId, runeObjectId], accepted.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, accepted.State.RunePools["P1"].Mana);
        Assert.Equal(1, accepted.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, accepted.State.RunePools["P1"].TotalPower);
        Assert.False(accepted.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("RUNE_DECK", accepted.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(accepted.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.RecycleRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertRecycleRuneOrdinaryMainPromptQueueAudit(accepted, runeObjectId, bottomRuneObjectId);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var currentPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var currentSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.RecycleRune, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.RecycleRune, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            NativeCommandWire.Bind(command, ref staleRawCommand),
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Empty(replay.State.PlayerZones["P1"].Base);
        Assert.Equal([bottomRuneObjectId, runeObjectId], replay.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, replay.State.RunePools["P1"].Mana);
        Assert.Equal(1, replay.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, replay.State.RunePools["P1"].TotalPower);
        Assert.False(replay.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("RUNE_DECK", replay.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(replay.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.RecycleRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertRecycleRuneOrdinaryMainPromptQueueAudit(replay, runeObjectId, bottomRuneObjectId);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = Assert.Single(journal.Entries, entry => !entry.Accepted);
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.RecycleRune, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(rejectedJournalEntry.RawCommand.Value));
        Assert.Equal(CommandTypes.RecycleRune, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, rejectedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var duplicateRejected = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            NativeCommandWire.Bind(command, ref staleRawCommand),
            CancellationToken.None);

        Assert.False(duplicateRejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateRejected.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateRejected.ErrorMessage);
        Assert.Empty(duplicateRejected.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicateRejected.State));
        Assert.Equal(replay.State.Tick, duplicateRejected.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(duplicateRejected.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(duplicateRejected.Snapshots));
        Assert.Empty(duplicateRejected.State.PlayerZones["P1"].Base);
        Assert.Equal([bottomRuneObjectId, runeObjectId], duplicateRejected.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, duplicateRejected.State.RunePools["P1"].Mana);
        Assert.Equal(1, duplicateRejected.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, duplicateRejected.State.RunePools["P1"].TotalPower);
        Assert.False(duplicateRejected.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("RUNE_DECK", duplicateRejected.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(duplicateRejected.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.RecycleRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertRecycleRuneOrdinaryMainPromptQueueAudit(duplicateRejected, runeObjectId, bottomRuneObjectId);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.RecycleRune,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        var conflict = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            NativeCommandWire.Bind(command, ref changedRawCommand),
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Empty(conflict.State.PlayerZones["P1"].Base);
        Assert.Equal([bottomRuneObjectId, runeObjectId], conflict.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, conflict.State.RunePools["P1"].Mana);
        Assert.Equal(1, conflict.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, conflict.State.RunePools["P1"].TotalPower);
        Assert.False(conflict.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("RUNE_DECK", conflict.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(conflict.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.RecycleRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertRecycleRuneOrdinaryMainPromptQueueAudit(conflict, runeObjectId, bottomRuneObjectId);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecycleRuneDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string runeObjectId = "P1-RUNE-RED-RECYCLE-RAW-IDEMPOTENCY";
        const string bottomRuneObjectId = "P1-RUNE-BOTTOM-RECYCLE-RAW-IDEMPOTENCY";
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [runeObjectId],
                    RuneDeck = [bottomRuneObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new(
                    runeObjectId,
                    isExhausted: true,
                    cardNo: "UNL-R01",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: "P1"),
                [bottomRuneObjectId] = new(
                    bottomRuneObjectId,
                    cardNo: "UNL-R03",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new("P1", "BASE"),
                [bottomRuneObjectId] = new("P1", "RUNE_DECK")
            }
        };
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new RecycleRuneCommand(runeObjectId);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.RecycleRune, prompt.EnabledActions());
        Assert.Contains(prompt.Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.RecycleRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        var rawCommand = PromptScopedRawCommand(CommandTypes.RecycleRune, prompt);
        const string clientIntentId = "intent-basic-rune-recycle-raw-idempotency";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref rawCommand),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["RUNE_RECYCLED", "POWER_GAINED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Empty(accepted.State.PlayerZones["P1"].Base);
        Assert.Equal([bottomRuneObjectId, runeObjectId], accepted.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, accepted.State.RunePools["P1"].Mana);
        Assert.Equal(1, accepted.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, accepted.State.RunePools["P1"].TotalPower);
        Assert.False(accepted.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("RUNE_DECK", accepted.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(accepted.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.RecycleRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertRecycleRuneOrdinaryMainPromptQueueAudit(accepted, runeObjectId, bottomRuneObjectId);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var currentPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var currentSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.RecycleRune, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.RecycleRune, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(journalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref rawCommand),
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Null(replay.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(accepted.State.RngCursor, replay.State.RngCursor);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(["RUNE_RECYCLED", "POWER_GAINED"], replay.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Empty(replay.State.PlayerZones["P1"].Base);
        Assert.Equal([bottomRuneObjectId, runeObjectId], replay.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, replay.State.RunePools["P1"].Mana);
        Assert.Equal(1, replay.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, replay.State.RunePools["P1"].TotalPower);
        Assert.False(replay.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("RUNE_DECK", replay.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(replay.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.RecycleRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertRecycleRuneOrdinaryMainPromptQueueAudit(replay, runeObjectId, bottomRuneObjectId);
        Assert.Single(journal.Entries);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.RecycleRune,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref changedRawCommand),
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Equal("该客户端行动编号已用于其他命令。", conflict.ErrorMessage);
        Assert.DoesNotContain(clientIntentId, conflict.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(accepted.State.RngCursor, conflict.State.RngCursor);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Empty(conflict.State.PlayerZones["P1"].Base);
        Assert.Equal([bottomRuneObjectId, runeObjectId], conflict.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, conflict.State.RunePools["P1"].Mana);
        Assert.Equal(1, conflict.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, conflict.State.RunePools["P1"].TotalPower);
        Assert.False(conflict.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("RUNE_DECK", conflict.State.ObjectLocations[runeObjectId].Zone);
        Assert.DoesNotContain(conflict.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.RecycleRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));
        AssertRecycleRuneOrdinaryMainPromptQueueAudit(conflict, runeObjectId, bottomRuneObjectId);
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    private static void AssertRecycleRuneOrdinaryMainPromptQueueAudit(
        ResolutionResult result,
        string runeObjectId,
        string bottomRuneObjectId)
    {
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, result.State.RunePools["P1"].TotalPower);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal([bottomRuneObjectId, runeObjectId], result.State.PlayerZones["P1"].RuneDeck);
        Assert.False(result.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal("P1", result.State.ObjectLocations[runeObjectId].PlayerId);
        Assert.Equal("RUNE_DECK", result.State.ObjectLocations[runeObjectId].Zone);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal("P1", snapshot.ActivePlayerId);
            Assert.Equal("P1", Assert.IsType<string>(snapshot.Timing["turnPlayerId"]));
            Assert.Equal(MatchPhases.Main, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Null(snapshot.Timing["priorityPlayerId"]);
            Assert.Null(snapshot.Timing["focusPlayerId"]);
            Assert.Empty(snapshot.Stack);

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }

        Assert.Equal("P1", result.Prompts["P1"].PlayerId);
        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal(PromptTypes.MainAction, result.Prompts["P1"].View?.Type);
        Assert.Equal(result.State.Tick, result.Prompts["P1"].SnapshotTick);
        Assert.Contains(CommandTypes.EndTurn, result.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(result.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.RecycleRune, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, runeObjectId, StringComparison.Ordinal)));

        Assert.Equal("P2", result.Prompts["P2"].PlayerId);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.DoesNotContain(CommandTypes.RecycleRune, result.Prompts["P2"].EnabledActions());
        Assert.Equal(result.State.Tick, result.Prompts["P2"].SnapshotTick);
    }

    [Fact]
    public async Task CoreRuleEngineRecyclesLegacyOwnedBasicRune()
    {
        const string runeObjectId = "P1-RUNE-RED-RECYCLE-LEGACY";
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [runeObjectId],
                    RuneDeck = ["P1-RUNE-BOTTOM-RECYCLE-LEGACY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new(
                    runeObjectId,
                    cardNo: "UNL-R01",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: ""),
                ["P1-RUNE-BOTTOM-RECYCLE-LEGACY"] = new(
                    "P1-RUNE-BOTTOM-RECYCLE-LEGACY",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-basic-rune-recycle-legacy", "P1", "RECYCLE_RUNE"),
            new RecycleRuneCommand(runeObjectId),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-RECYCLE-LEGACY", runeObjectId], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal("RUNE_DECK", result.State.ObjectLocations[runeObjectId].Zone);
    }

    [Fact]
    public async Task CoreRuleEngineRecyclesBasicRuneAndReconcilesObjectLocations()
    {
        const string runeObjectId = "P1-RUNE-RED-RECYCLE-LOCATION";
        const string bottomRuneObjectId = "P1-RUNE-BOTTOM-RECYCLE-LOCATION";
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [runeObjectId],
                    RuneDeck = [bottomRuneObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new(
                    runeObjectId,
                    cardNo: "UNL-R01",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: "P1",
                    controllerId: "P1"),
                [bottomRuneObjectId] = new(
                    bottomRuneObjectId,
                    cardNo: "UNL-R03",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-basic-rune-recycle-location", "P1", "RECYCLE_RUNE"),
            new RecycleRuneCommand(runeObjectId),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal([bottomRuneObjectId, runeObjectId], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal("RUNE_DECK", result.State.ObjectLocations[runeObjectId].Zone);
        Assert.Equal("RUNE_DECK", result.State.ObjectLocations[bottomRuneObjectId].Zone);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPortalpalRescueBanishPlayBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-portalpal-rescue-banish-play-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-PORTALPAL-BASE-KEEPER", "P1-PORTALPAL-TARGET-001"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Banished);
        Assert.Equal(0, result.FinalState.CardObjects["P1-PORTALPAL-TARGET-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P1-PORTALPAL-TARGET-001"].Power);
        Assert.Empty(result.FinalState.CardObjects["P1-PORTALPAL-TARGET-001"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysArcaneShiftBanishFriendlyDamageEnemy()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-arcane-shift-banish-friendly-damage-enemy.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-ARCANE-SHIFT-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P1-SPELL-ARCANE-SHIFT"], result.FinalState.PlayerZones["P1"].Banished);
        Assert.Equal(0, result.FinalState.CardObjects["P1-ARCANE-SHIFT-FRIENDLY-001"].Damage);
        Assert.Equal(3, result.FinalState.CardObjects["P1-ARCANE-SHIFT-FRIENDLY-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P2-ARCANE-SHIFT-ENEMY-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePortalpalRescueResolutionSkipsOpponentControlledFriendlyZoneTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-PORTALPAL-RESCUE-DIRTY-BATTLEFIELD",
                    "P1",
                    "P1-SPELL-PORTALPAL-RESCUE",
                    "PORTALPAL_RESCUE_BANISH_FRIENDLY_UNIT_PLAY_TO_BASE",
                    "OGN·102/298",
                    ["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"] = new(
                    "P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 2,
                    damage: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-portalpal-rescue-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-portalpal-rescue-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"], p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Banished);
        Assert.Equal(["P1-SPELL-PORTALPAL-RESCUE"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(1, p2Pass.State.CardObjects["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"].Damage);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_BANISHED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPortalpalRescueAgainstEnemyUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PORTALPAL-RESCUE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-PORTALPAL-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-PORTALPAL-ENEMY-001"] = new(
                    "P2-PORTALPAL-ENEMY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-portalpal-rescue-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PORTALPAL-RESCUE", "OGN·102/298", ["P2-PORTALPAL-ENEMY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-PORTALPAL-RESCUE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-PORTALPAL-ENEMY-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsArcaneShiftAgainstEnemyFirstTarget()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ARCANE-SHIFT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-ARCANE-SHIFT-ENEMY-001", "P2-ARCANE-SHIFT-ENEMY-002"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-ARCANE-SHIFT-ENEMY-001"] = new(
                    "P2-ARCANE-SHIFT-ENEMY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-ARCANE-SHIFT-ENEMY-002"] = new(
                    "P2-ARCANE-SHIFT-ENEMY-002",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-arcane-shift-enemy-first-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-ARCANE-SHIFT",
                "SFD·200/221",
                ["P2-ARCANE-SHIFT-ENEMY-001", "P2-ARCANE-SHIFT-ENEMY-002"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-ARCANE-SHIFT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsArcaneShiftAgainstBaseEnemySecondTarget()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ARCANE-SHIFT"],
                    Battlefields = ["P1-ARCANE-SHIFT-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-ARCANE-SHIFT-BASE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-ARCANE-SHIFT-FRIENDLY-001"] = new(
                    "P1-ARCANE-SHIFT-FRIENDLY-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-ARCANE-SHIFT-BASE-001"] = new(
                    "P2-ARCANE-SHIFT-BASE-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-arcane-shift-base-enemy-second-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-ARCANE-SHIFT",
                "SFD·200/221",
                ["P1-ARCANE-SHIFT-FRIENDLY-001", "P2-ARCANE-SHIFT-BASE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-ARCANE-SHIFT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHuntingRhythmBanishPlayBattlefield()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hunting-rhythm-banish-play-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P1-HUNTING-RHYTHM-FIELD-KEEPER", "P1-HUNTING-RHYTHM-TARGET-001"],
            result.FinalState.PlayerZones["P1"].Battlefields.Where(id =>
                result.FinalState.CardObjects[id].Tags.Contains(CardObjectTags.UnitCard)).ToArray());
        Assert.Empty(result.FinalState.PlayerZones["P1"].Banished);
        Assert.Equal(0, result.FinalState.CardObjects["P1-HUNTING-RHYTHM-TARGET-001"].Damage);
        Assert.Equal(5, result.FinalState.CardObjects["P1-HUNTING-RHYTHM-TARGET-001"].Power);
        Assert.Empty(result.FinalState.CardObjects["P1-HUNTING-RHYTHM-TARGET-001"].UntilEndOfTurnEffects);
    }
}
