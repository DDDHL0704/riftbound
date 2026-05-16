using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LuxResourceSkillTests
{
    private const string LuxObjectId = "P1-LUX";
    private const string SpellObjectId = "P1-SPELL-BULLET-TIME";
    private const string EnemyObjectId = "P2-LUX-TEST-UNIT";
    private const string BulletTimeCardNo = "OGN·268/298";
    private const string ArenaCouncilorCardNo = "UNL-001/219";

    private static string LuxResourceAction => $"{P4ActivatedAbilityCatalog.LuxSpellOnlyResourceActionPrefix}{LuxObjectId}";

    [Fact]
    public void CatalogExposesLuxSpellOnlyResourceSkill()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.LuxResourceAbilityId,
            out var ability));

        Assert.Equal(P4ActivatedAbilityCatalog.LuxCardNo, ability.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxResourceAbilityEffectKind, ability.EffectKind);
        Assert.True(ability.IsResourceSkill);
        Assert.True(ability.PaymentOnlyResource);
        Assert.True(ability.ReactionSpeed);
        Assert.True(ability.ExhaustsSourceAsCost);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, ability.GeneratedMana);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxSpellOnlyResourceRestriction, ability.ResourceRestriction);
    }

    [Fact]
    public void LuxSpellOnlyResourcePromptMakesShortManaSpellPlayable()
    {
        var state = BuildPlayState(mana: 0);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]);
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, LuxResourceAction, StringComparison.Ordinal));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["paymentResourceChoices"]);
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, LuxResourceAction, StringComparison.Ordinal));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, paymentResourcePowerByChoice[LuxResourceAction]["mana"]);
        Assert.Equal(true, paymentResourcePowerByChoice[LuxResourceAction]["paymentOnly"]);
        Assert.Equal(true, paymentResourcePowerByChoice[LuxResourceAction]["spellOnly"]);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxSpellOnlyResourceRestriction, paymentResourcePowerByChoice[LuxResourceAction]["resourceRestriction"]);
        Assert.Equal(0, sourceRequirement["availableMana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, sourceRequirement["availableManaWithPaymentResources"]);
    }

    [Fact]
    public void LuxSpellOnlyResourcePromptDoesNotMakeUnitPlayable()
    {
        var state = BuildPlayState(cardNo: ArenaCouncilorCardNo, mana: 3);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.False(playCandidate.Enabled);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
    }

    [Fact]
    public async Task LuxSpellOnlyResourcePaysSpellManaExhaustsSourceAndCleansLeftover()
    {
        var state = BuildPlayState(mana: 0);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-lux-spell-only-resource", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                SpellObjectId,
                BulletTimeCardNo,
                [],
                OptionalCosts: [LuxResourceAction]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects[LuxObjectId].IsExhausted);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(SpellObjectId, stackItem.SourceObjectId);
        Assert.Equal(BulletTimeCardNo, stackItem.CardNo);
        Assert.Equal(
            [
                "CARD_PLAYED",
                "ABILITY_ACTIVATED",
                "UNIT_EXHAUSTED",
                "MANA_GAINED",
                "TEMPORARY_PAYMENT_RESOURCE_SPENT",
                "TEMPORARY_PAYMENT_RESOURCE_CLEARED",
                "COST_PAID",
                "STACK_ITEM_ADDED"
            ],
            result.Events.Select(gameEvent => gameEvent.Kind));

        var activatedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Equal(P4ActivatedAbilityCatalog.LuxResourceAbilityId, activatedEvent.Payload["abilityId"]);
        Assert.Equal("no-ordinary-stack-item", activatedEvent.Payload["stackPolicy"]);
        Assert.Equal(true, activatedEvent.Payload["generatedResourceCannotBeTargetedAsResponse"]);
        var manaEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, manaEvent.Payload["mana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, manaEvent.Payload["manaAfter"]);
        var spendEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.Equal(1, spendEvent.Payload["consumedMana"]);
        Assert.Equal(1, spendEvent.Payload["remainingMana"]);
        var cleanupEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
        Assert.Equal(1, cleanupEvent.Payload["remainingManaBeforeCleanup"]);

        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([LuxResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([LuxResourceAction], Assert.IsType<string[]>(costEvent.Payload["luxSpellOnlyResourceActions"]));
        Assert.Equal([LuxObjectId], Assert.IsType<string[]>(costEvent.Payload["luxSpellOnlyResourceSourceObjectIds"]));
        Assert.Equal(P4ActivatedAbilityCatalog.LuxGeneratedMana, costEvent.Payload["luxSpellOnlyGeneratedMana"]);
        Assert.Equal(1, costEvent.Payload["luxSpellOnlyConsumedMana"]);
        Assert.Equal(1, costEvent.Payload["luxSpellOnlyRemainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
    }

    [Theory]
    [InlineData("non-spell")]
    [InlineData("exhausted-source")]
    [InlineData("wrong-source-card")]
    [InlineData("unnecessary-resource")]
    [InlineData("duplicate-resource")]
    public async Task LuxSpellOnlyResourceRejectsInvalidCommandsWithoutMutation(string caseName)
    {
        var state = caseName switch
        {
            "non-spell" => BuildPlayState(cardNo: ArenaCouncilorCardNo, mana: 0),
            "exhausted-source" => BuildPlayState(luxOverride: LuxCard() with { IsExhausted = true }),
            "wrong-source-card" => BuildPlayState(luxOverride: LuxCard() with { CardNo = "OGS·015/024" }),
            "unnecessary-resource" => BuildPlayState(mana: 1),
            _ => BuildPlayState()
        };
        var command = caseName == "non-spell"
            ? new PlayCardCommand(
                SpellObjectId,
                ArenaCouncilorCardNo,
                [],
                OptionalCosts: [LuxResourceAction])
            : new PlayCardCommand(
                SpellObjectId,
                BulletTimeCardNo,
                [],
                OptionalCosts: caseName == "duplicate-resource" ? [LuxResourceAction, LuxResourceAction] : [LuxResourceAction]);
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-lux-invalid-{caseName}", "P1", CommandTypes.PlayCard),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    private static MatchState BuildPlayState(
        string cardNo = BulletTimeCardNo,
        int mana = 0,
        CardObjectState? luxOverride = null)
    {
        var sourceCard = string.Equals(cardNo, ArenaCouncilorCardNo, StringComparison.Ordinal)
            ? ArenaCouncilorCard()
            : BulletTimeCard();
        var lux = luxOverride ?? LuxCard();
        return new MatchState(
            roomId: "lux-resource-skill-test",
            tick: 40,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "Alice",
                ["P2"] = "Bob"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new RunePool(mana, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [SpellObjectId],
                    Base = [LuxObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [EnemyObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [SpellObjectId] = sourceCard,
                [LuxObjectId] = lux,
                [EnemyObjectId] = EnemyUnit()
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [SpellObjectId] = new("P1", "HAND"),
                [LuxObjectId] = new("P1", "BASE"),
                [EnemyObjectId] = new("P2", "BATTLEFIELD", "P2-MAIN")
            });
    }

    private static CardObjectState BulletTimeCard()
    {
        return new CardObjectState(
            SpellObjectId,
            cardNo: BulletTimeCardNo,
            tags: [CardObjectTags.SpellCard],
            manaCost: 1,
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState ArenaCouncilorCard()
    {
        return new CardObjectState(
            SpellObjectId,
            cardNo: ArenaCouncilorCardNo,
            power: 3,
            tags: [CardObjectTags.UnitCard],
            manaCost: 5,
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState LuxCard()
    {
        return new CardObjectState(
            LuxObjectId,
            cardNo: P4ActivatedAbilityCatalog.LuxCardNo,
            power: 2,
            tags: [CardObjectTags.UnitCard],
            manaCost: 3,
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState EnemyUnit()
    {
        return new CardObjectState(
            EnemyObjectId,
            cardNo: "SFD·125/221",
            power: 5,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P2",
            controllerId: "P2");
    }
}
