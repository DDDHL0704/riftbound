using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task P6LegendAbilityCatalogAuditsImplementedLegendActionRepresentativesAgainstOfficialText()
    {
        var surfaces = P6LegendAbilityCatalog.GetImplementedLegendActionSurfaces();

        Assert.Equal(5, surfaces.Count);
        Assert.Empty(P6LegendAbilityCatalog.GetDeferredSurfaces());
        Assert.Contains(surfaces, surface => surface.RequiresTarget);
        Assert.Contains(surfaces, surface => !surface.RequiresTarget);
        Assert.All(surfaces, surface =>
        {
            Assert.False(P4ActivatedAbilityCatalog.TryGetByAbilityId(surface.AbilityId, out _));
            Assert.StartsWith("LEGEND_DEFERRED_", surface.RetiredDeferredSurfaceId, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(surface.Reason));
            Assert.Contains("LEGEND_ACTION_DOMAIN", surface.Reason, StringComparison.Ordinal);
        });

        var officialCatalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var functionalUnits = FunctionalUnitBuilder.Build(officialCatalog.Cards);
        var implementedBehaviors = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(
            officialCatalog.Cards,
            CardBehaviorRegistry.GetAll()
                .Select(definition => new ImplementedCardBehavior(
                    definition.CardNo,
                    definition.EffectKind,
                    definition.DisplayName))
                .ToArray());
        var behaviorSpecs = BehaviorSpecCatalogBuilder.Build(
            officialCatalog.Cards,
            functionalUnits,
            implementedBehaviors);

        foreach (var surface in surfaces)
        {
            var officialCard = officialCatalog.Cards.Single(card =>
                string.Equals(card.CardNo, surface.SourceCardNo, StringComparison.Ordinal));
            Assert.Equal("传奇", officialCard.CardCategoryName);
            Assert.Contains(surface.OfficialTextAnchor, officialCard.CardEffect, StringComparison.Ordinal);
            Assert.NotEmpty(RuleTextParser.Parse(officialCard).ActivatedAbilities);
            Assert.False(CardBehaviorRegistry.TryGetByCardNo(surface.SourceCardNo, out _));
            Assert.Contains(behaviorSpecs, spec =>
                string.Equals(spec.CardNo, surface.SourceCardNo, StringComparison.Ordinal)
                && string.Equals(spec.Status, BehaviorImplementationStatuses.Implemented, StringComparison.Ordinal)
                && string.Equals(spec.ImplementedEffectKind, OfficialRuleDomainBehaviorCatalog.LegendActionDomainEffectKind, StringComparison.Ordinal));
        }
    }

    public static IEnumerable<object[]> P6ImplementedLegendActionSurfaceData()
    {
        return P6LegendAbilityCatalog.GetImplementedLegendActionSurfaces()
            .Select(surface => new object[] { surface });
    }

    [Theory]
    [MemberData(nameof(P6ImplementedLegendActionSurfaceData))]
    public async Task P6ActivateAbilityCommandRejectsImplementedLegendActionRepresentativesOutsideActivateAbilityRegistry(
        P6ImplementedLegendActionSurface surface)
    {
        var targetObjectIds = surface.RequiresTarget
            ? new[] { "P1-LEGEND-TARGET-UNIT" }
            : Array.Empty<string>();
        var state = PunishmentState(mana: 10) with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 10,
                ["P2"] = 0
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    LegendZone = ["P1-LEGEND-ACTION-SOURCE"],
                    Base = ["P1-LEGEND-TARGET-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(10, 10),
                ["P2"] = RunePool.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-ACTION-SOURCE"] = new(
                    "P1-LEGEND-ACTION-SOURCE",
                    power: 0,
                    cardNo: surface.SourceCardNo,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-TARGET-UNIT"] = new(
                    "P1-LEGEND-TARGET-UNIT",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-p6-legend-action-activate-ability-reject-{surface.AbilityId}", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-LEGEND-ACTION-SOURCE",
                surface.AbilityId,
                targetObjectIds),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, result.ErrorCode);
        Assert.Equal("当前启动技能路径尚未由服务端开放。", result.ErrorMessage);
        Assert.DoesNotContain("ACTIVATE_ABILITY", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(10, 10), result.State.RunePools["P1"]);
        Assert.Equal(10, result.State.PlayerExperience["P1"]);
        Assert.Equal(["P1-LEGEND-ACTION-SOURCE"], result.State.PlayerZones["P1"].LegendZone);
        Assert.Equal(["P1-LEGEND-TARGET-UNIT"], result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects["P1-LEGEND-ACTION-SOURCE"].IsExhausted);
        Assert.Equal(0, result.State.CardObjects["P1-LEGEND-TARGET-UNIT"].Damage);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P79LegendActSpendsExperienceExhaustsLegendAndDraws()
    {
        var state = LegendActState(experience: 3) with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-POPPY"] = new("P1", "LEGEND"),
                ["P1-LEGEND-DRAW-001"] = new("P1", "MAIN_DECK")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-POPPY",
                "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
                [],
                ["SPEND_EXPERIENCE:3"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.True(result.State.CardObjects["P1-LEGEND-POPPY"].IsExhausted);
        Assert.Empty(result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-LEGEND-DRAW-001"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal("HAND", result.State.ObjectLocations["P1-LEGEND-DRAW-001"].Zone);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_ABILITY_ACTIVATED", StringComparison.Ordinal));
        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("LEGEND_ACT", costPaidEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-LEGEND-POPPY", costPaidEvent.Payload["sourceObjectId"]);
        Assert.Equal("LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW", costPaidEvent.Payload["abilityId"]);
        Assert.Equal(0, costPaidEvent.Payload["totalManaCost"]);
        Assert.Equal(3, costPaidEvent.Payload["experienceCost"]);
        Assert.Equal(0, costPaidEvent.Payload["remainingExperience"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_SPENT", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_EXHAUSTED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendActRejectsInsufficientExperienceWithoutSideEffects()
    {
        var state = LegendActState(experience: 2);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-legend-act-insufficient", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-POPPY",
                "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
                [],
                ["SPEND_EXPERIENCE:3"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(2, result.State.PlayerExperience["P1"]);
        Assert.False(result.State.CardObjects["P1-LEGEND-POPPY"].IsExhausted);
        Assert.Equal(["P1-LEGEND-DRAW-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task P79LegendActRejectsUnknownAbilityWithChineseError()
    {
        var state = LegendActState(experience: 3);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-legend-act-unknown-ability", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-POPPY",
                "LEGEND_UNKNOWN_ABILITY",
                [],
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal("当前传奇行动尚未由服务端开放。", result.ErrorMessage);
        Assert.DoesNotContain("LEGEND_ACT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(3, result.State.PlayerExperience["P1"]);
        Assert.False(result.State.CardObjects["P1-LEGEND-POPPY"].IsExhausted);
        Assert.Equal(["P1-LEGEND-DRAW-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task P79LegendActRejectsSourceOutsideLegendZoneWithChineseError()
    {
        var state = LegendActState(experience: 3);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-legend-act-source-outside-zone", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-DRAW-001",
                "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
                [],
                ["SPEND_EXPERIENCE:3"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("传奇行动需要选择自己传奇区中的传奇。", result.ErrorMessage);
        Assert.DoesNotContain("LEGEND_ACT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(3, result.State.PlayerExperience["P1"]);
        Assert.False(result.State.CardObjects["P1-LEGEND-POPPY"].IsExhausted);
        Assert.Equal(["P1-LEGEND-DRAW-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task P79LegendActRejectsOpponentControlledSourceWithChineseError()
    {
        var state = LegendActState(experience: 3);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-POPPY"] = cardObjects["P1-LEGEND-POPPY"] with
        {
            ControllerId = "P2"
        };
        state = state with
        {
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-legend-act-opponent-controlled-source", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-POPPY",
                "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
                [],
                ["SPEND_EXPERIENCE:3"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("传奇行动只能选择当前玩家控制的传奇。", result.ErrorMessage);
        Assert.DoesNotContain("LEGEND_ACT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(3, result.State.PlayerExperience["P1"]);
        Assert.False(result.State.CardObjects["P1-LEGEND-POPPY"].IsExhausted);
        Assert.Equal("P2", result.State.CardObjects["P1-LEGEND-POPPY"].ControllerId);
        Assert.Equal(["P1-LEGEND-DRAW-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task P79LegendActRejectsSourceWithoutCardNoWithChineseError()
    {
        var state = LegendActState(experience: 3);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-POPPY"] = cardObjects["P1-LEGEND-POPPY"] with
        {
            CardNo = null
        };
        state = state with
        {
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-legend-act-source-without-card-no", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-POPPY",
                "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
                [],
                ["SPEND_EXPERIENCE:3"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal("传奇行动需要服务端已确认的传奇牌信息。", result.ErrorMessage);
        Assert.DoesNotContain("LEGEND_ACT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(3, result.State.PlayerExperience["P1"]);
        Assert.False(result.State.CardObjects["P1-LEGEND-POPPY"].IsExhausted);
        Assert.Equal(["P1-LEGEND-DRAW-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task P79LegendActRejectsSourceWithoutRequestedAbilityWithChineseError()
    {
        var state = LegendActiveAbilityState("UNL-237/219", "P1-LEGEND-POPPY", mana: 2);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-legend-act-source-without-ability", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-POPPY",
                "LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT",
                ["P1-LEGEND-BATTLEFIELD-UNIT"],
                ["SPEND_MANA:2"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal("该传奇没有服务端已开放的对应传奇行动。", result.ErrorMessage);
        Assert.DoesNotContain("LEGEND_ACT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(2, result.State.RunePools["P1"].Mana);
        Assert.False(result.State.CardObjects["P1-LEGEND-POPPY"].IsExhausted);
        Assert.Contains("P1-LEGEND-BATTLEFIELD-UNIT", result.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("P1-LEGEND-BATTLEFIELD-UNIT", result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task P79LegendActRejectsExhaustedSourceWithChineseError()
    {
        var state = LegendActState(experience: 3);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-POPPY"] = cardObjects["P1-LEGEND-POPPY"] with
        {
            IsExhausted = true
        };
        state = state with
        {
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-legend-act-exhausted-source", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-POPPY",
                "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
                [],
                ["SPEND_EXPERIENCE:3"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("传奇行动需要选择未横置的传奇。", result.ErrorMessage);
        Assert.DoesNotContain("LEGEND_ACT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(3, result.State.PlayerExperience["P1"]);
        Assert.True(result.State.CardObjects["P1-LEGEND-POPPY"].IsExhausted);
        Assert.Equal(["P1-LEGEND-DRAW-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task P79LegendActMovesFriendlyUnitWithYasuo()
    {
        var state = LegendActiveAbilityState("FND-259/298", "P1-LEGEND-YASUO", mana: 2);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-yasuo-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-YASUO",
                "LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT",
                ["P1-LEGEND-BATTLEFIELD-UNIT"],
                ["SPEND_MANA:2"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.True(result.State.CardObjects["P1-LEGEND-YASUO"].IsExhausted);
        Assert.Contains("P1-LEGEND-BATTLEFIELD-UNIT", result.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("P1-LEGEND-BATTLEFIELD-UNIT", result.State.PlayerZones["P1"].Battlefields);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendActYasuoRejectsTargetWithoutCardNo()
    {
        var state = LegendActiveAbilityState("FND-259/298", "P1-LEGEND-YASUO", mana: 2);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-BATTLEFIELD-UNIT"] = cardObjects["P1-LEGEND-BATTLEFIELD-UNIT"] with
        {
            CardNo = null
        };
        state = state with
        {
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-yasuo-legend-act-unknown-target", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-YASUO",
                "LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT",
                ["P1-LEGEND-BATTLEFIELD-UNIT"],
                ["SPEND_MANA:2"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(2, result.State.RunePools["P1"].Mana);
        Assert.False(result.State.CardObjects["P1-LEGEND-YASUO"].IsExhausted);
        Assert.Contains("P1-LEGEND-BATTLEFIELD-UNIT", result.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("P1-LEGEND-BATTLEFIELD-UNIT", result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task P79LegendActYasuoRejectsOpponentControlledTargetInPlayerZone()
    {
        var state = LegendActiveAbilityState("FND-259/298", "P1-LEGEND-YASUO", mana: 2);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-BATTLEFIELD-UNIT"] = cardObjects["P1-LEGEND-BATTLEFIELD-UNIT"] with
        {
            OwnerId = "P1",
            ControllerId = "P2"
        };
        state = state with
        {
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-yasuo-legend-act-opponent-controlled-target", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-YASUO",
                "LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT",
                ["P1-LEGEND-BATTLEFIELD-UNIT"],
                ["SPEND_MANA:2"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(2, result.State.RunePools["P1"].Mana);
        Assert.False(result.State.CardObjects["P1-LEGEND-YASUO"].IsExhausted);
        Assert.Equal("P2", result.State.CardObjects["P1-LEGEND-BATTLEFIELD-UNIT"].ControllerId);
        Assert.Contains("P1-LEGEND-BATTLEFIELD-UNIT", result.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("P1-LEGEND-BATTLEFIELD-UNIT", result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task P79LegendActAcceptsYasuoReprintFunctionalUnit()
    {
        var state = LegendActiveAbilityState("OGN·305/298", "P1-LEGEND-YASUO-REPRINT", mana: 2);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-yasuo-reprint-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-YASUO-REPRINT",
                "LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT",
                ["P1-LEGEND-BATTLEFIELD-UNIT"],
                ["SPEND_MANA:2"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(result.State.CardObjects["P1-LEGEND-YASUO-REPRINT"].IsExhausted);
        Assert.Contains("P1-LEGEND-BATTLEFIELD-UNIT", result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task P79LegendActGrantsBoonWithLeeSin()
    {
        var state = LegendActiveAbilityState("OGN·257/298", "P1-LEGEND-LEE-SIN", mana: 1);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-lee-sin-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-LEE-SIN",
                "LEGEND_PAY_1_EXHAUST_GRANT_BOON",
                ["P1-LEGEND-BASE-UNIT"],
                ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.True(result.State.CardObjects["P1-LEGEND-LEE-SIN"].IsExhausted);
        var target = result.State.CardObjects["P1-LEGEND-BASE-UNIT"];
        Assert.Equal(3, target.Power);
        Assert.Contains(CardObjectTags.Boon, target.Tags);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendActCreatesMinionWithViktor()
    {
        var state = LegendActiveAbilityState("FND-265/298", "P1-LEGEND-VIKTOR", mana: 1);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-viktor-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-VIKTOR",
                "LEGEND_PAY_1_EXHAUST_CREATE_MINION",
                [],
                ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Single(result.State.StackItems);
        Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_TOKEN_CREATED");
        var activationEvents = result.Events;
        result = await OfficialGraveyardRecastTests.Top(result.State);
        result = result with { Events = activationEvents.Concat(result.Events).ToArray() };
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.True(result.State.CardObjects["P1-LEGEND-VIKTOR"].IsExhausted);
        Assert.Contains("P1-LEGEND-VIKTOR-TOKEN-001", result.State.PlayerZones["P1"].Base);
        var token = result.State.CardObjects["P1-LEGEND-VIKTOR-TOKEN-001"];
        Assert.Equal(1, token.Power);
        Assert.True(token.IsExhausted);
        Assert.Contains(CardObjectTags.UnitCard, token.Tags);
        Assert.Contains(CardObjectTags.MinionTokenFamily, token.Tags);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendActGrantsRoamWithMissFortune()
    {
        var state = LegendActiveAbilityState("OGN·267/298", "P1-LEGEND-MISS-FORTUNE", mana: 0);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-miss-fortune-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-MISS-FORTUNE",
                "LEGEND_EXHAUST_GRANT_ROAM",
                ["P1-LEGEND-BASE-UNIT"],
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(result.State.CardObjects["P1-LEGEND-MISS-FORTUNE"].IsExhausted);
        Assert.Contains("ROAM", result.State.CardObjects["P1-LEGEND-BASE-UNIT"].UntilEndOfTurnEffects);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ROAM_GRANTED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendActKhazixSpendsExperienceToGrantBoon()
    {
        var state = LegendActiveAbilityState("UNL-201/219", "P1-LEGEND-KHAZIX", mana: 0) with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 1,
                ["P2"] = 0
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-khazix-boon-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-KHAZIX",
                "LEGEND_SPEND_1_EXPERIENCE_EXHAUST_GRANT_BOON",
                ["P1-LEGEND-BASE-UNIT"],
                ["SPEND_EXPERIENCE:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.True(result.State.CardObjects["P1-LEGEND-KHAZIX"].IsExhausted);
        Assert.Contains(CardObjectTags.Boon, result.State.CardObjects["P1-LEGEND-BASE-UNIT"].Tags);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EXPERIENCE_SPENT", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendActKhazixMovesDormantBattlefieldUnitToBase()
    {
        var state = LegendActiveAbilityState("UNL-201/219", "P1-LEGEND-KHAZIX", mana: 0) with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 2,
                ["P2"] = 0
            },
            CardObjects = LegendActiveAbilityState("UNL-201/219", "P1-LEGEND-KHAZIX", mana: 0)
                .CardObjects
                .ToDictionary(
                    entry => entry.Key,
                    entry => string.Equals(entry.Key, "P1-LEGEND-BATTLEFIELD-UNIT", StringComparison.Ordinal)
                        ? entry.Value with { IsExhausted = true }
                        : entry.Value,
                    StringComparer.Ordinal)
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-khazix-move-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-KHAZIX",
                "LEGEND_SPEND_2_EXPERIENCE_EXHAUST_MOVE_DORMANT_UNIT_TO_BASE",
                ["P1-LEGEND-BATTLEFIELD-UNIT"],
                ["SPEND_EXPERIENCE:2"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.Contains("P1-LEGEND-BATTLEFIELD-UNIT", result.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("P1-LEGEND-BATTLEFIELD-UNIT", result.State.PlayerZones["P1"].Battlefields);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendActPykeReturnsBattlefieldUnitAndCreatesCoin()
    {
        var state = LegendActiveAbilityState("UNL-185/219", "P1-LEGEND-PYKE", mana: 1);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-pyke-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-PYKE",
                "LEGEND_PAY_1_EXHAUST_RECALL_BATTLEFIELD_UNIT_CREATE_COIN",
                ["P1-LEGEND-BATTLEFIELD-UNIT"],
                ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.True(result.State.CardObjects["P1-LEGEND-PYKE"].IsExhausted);
        Assert.Contains("P1-LEGEND-BATTLEFIELD-UNIT", result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("P1-LEGEND-BATTLEFIELD-UNIT", result.State.PlayerZones["P1"].Battlefields);
        Assert.Contains("P1-LEGEND-PYKE-TOKEN-001", result.State.PlayerZones["P1"].Base);
        var token = result.State.CardObjects["P1-LEGEND-PYKE-TOKEN-001"];
        Assert.True(token.IsExhausted);
        Assert.Contains(CardObjectTags.EquipmentCard, token.Tags);
        Assert.Contains("反应", token.Tags);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_RETURNED_TO_HAND", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendActJaxAttachesUnattachedArmament()
    {
        var state = JaxLegendActiveAbilityState("SFD·193/221", "P1-LEGEND-JAX", mana: 1, armamentAttachedToObjectId: null);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-jax-attach-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-JAX",
                "LEGEND_PAY_1_EXHAUST_ATTACH_UNATTACHED_ARMAMENT",
                ["P1-LEGEND-BASE-UNIT", "P1-JAX-ARMAMENT"],
                ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        result = await LegendAttachmentTestDriver.FinishAsync(result);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.True(result.State.CardObjects["P1-LEGEND-JAX"].IsExhausted);
        Assert.Equal("P1-LEGEND-BASE-UNIT", result.State.CardObjects["P1-JAX-ARMAMENT"].AttachedToObjectId);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("P1-JAX-ARMAMENT", attachedEvent.Payload["equipmentObjectId"]);
        Assert.Equal("P1-LEGEND-BASE-UNIT", attachedEvent.Payload["attachedToObjectId"]);
    }

    [Fact]
    public async Task P79LegendActJaxReattachesAttachedArmament()
    {
        var state = JaxLegendActiveAbilityState(
            "SFD·245/221",
            "P1-LEGEND-JAX-REPRINT",
            mana: 0,
            armamentAttachedToObjectId: "P1-LEGEND-BATTLEFIELD-UNIT");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-jax-reattach-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-JAX-REPRINT",
                "LEGEND_EXHAUST_REATTACH_ATTACHED_ARMAMENT",
                ["P1-LEGEND-BASE-UNIT", "P1-JAX-ARMAMENT"],
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        result = await LegendAttachmentTestDriver.FinishAsync(result);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.True(result.State.CardObjects["P1-LEGEND-JAX-REPRINT"].IsExhausted);
        Assert.Equal("P1-LEGEND-BASE-UNIT", result.State.CardObjects["P1-JAX-ARMAMENT"].AttachedToObjectId);
        Assert.Contains(result.Events, gameEvent => gameEvent.Kind == "EQUIPMENT_DETACHED");
        var reattachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-BATTLEFIELD-UNIT", reattachedEvent.Payload["previousAttachedToObjectId"]);
        Assert.Equal("P1-LEGEND-BASE-UNIT", reattachedEvent.Payload["attachedToObjectId"]);
    }

    [Fact]
    public async Task P79BattlefieldForgeGrantsLegendArmamentAttach()
    {
        var state = BattlefieldLegendAttachArmamentState(includeForge: true, armamentAttachedToObjectId: null);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-forge-legend-attach", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-PORO-FORGE",
                "LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD",
                ["P1-LEGEND-BASE-UNIT", "P1-PORO-FORGE-ARMAMENT"],
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        result = await LegendAttachmentTestDriver.FinishAsync(result);
        Assert.True(result.State.CardObjects["P1-LEGEND-PORO-FORGE"].IsExhausted);
        Assert.Equal("P1-LEGEND-BASE-UNIT", result.State.CardObjects["P1-PORO-FORGE-ARMAMENT"].AttachedToObjectId);
        var triggerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONTROLLED_LEGEND_ATTACH_ARMAMENT", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-PORO-FORGE", triggerEvent.Payload["battlefieldObjectId"]);
        Assert.Equal("SFD·208/221", triggerEvent.Payload["battlefieldCardNo"]);
        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("P1-PORO-FORGE-ARMAMENT", attachedEvent.Payload["equipmentObjectId"]);
        Assert.Equal("P1-LEGEND-BASE-UNIT", attachedEvent.Payload["attachedToObjectId"]);
    }

    [Fact]
    public async Task P79BattlefieldForgeReattachesControlledArmament()
    {
        var state = BattlefieldLegendAttachArmamentState(
            includeForge: true,
            armamentAttachedToObjectId: "P1-LEGEND-BATTLEFIELD-UNIT");

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-forge-legend-reattach", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-PORO-FORGE",
                "LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD",
                ["P1-LEGEND-BASE-UNIT", "P1-PORO-FORGE-ARMAMENT"],
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        result = await LegendAttachmentTestDriver.FinishAsync(result);
        Assert.True(result.State.CardObjects["P1-LEGEND-PORO-FORGE"].IsExhausted);
        Assert.Equal("P1-LEGEND-BASE-UNIT", result.State.CardObjects["P1-PORO-FORGE-ARMAMENT"].AttachedToObjectId);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONTROLLED_LEGEND_ATTACH_ARMAMENT", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => gameEvent.Kind == "EQUIPMENT_DETACHED");
        var reattachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-BATTLEFIELD-UNIT", reattachedEvent.Payload["previousAttachedToObjectId"]);
        Assert.Equal("P1-LEGEND-BASE-UNIT", reattachedEvent.Payload["attachedToObjectId"]);
    }

    [Fact]
    public async Task P79BattlefieldForgeLegendAttachRejectedWithoutControlledForge()
    {
        var state = BattlefieldLegendAttachArmamentState(includeForge: false, armamentAttachedToObjectId: null);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-forge-legend-attach-rejected", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-PORO-FORGE",
                "LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD",
                ["P1-LEGEND-BASE-UNIT", "P1-PORO-FORGE-ARMAMENT"],
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.False(result.State.CardObjects["P1-LEGEND-PORO-FORGE"].IsExhausted);
        Assert.Null(result.State.CardObjects["P1-PORO-FORGE-ARMAMENT"].AttachedToObjectId);
    }

    [Fact]
    public async Task P79BattlefieldForgeLegendAttachRejectsOpponentOwnedForge()
    {
        var state = BattlefieldLegendAttachArmamentState(includeForge: true, armamentAttachedToObjectId: null);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-BATTLEFIELD-PORO-FORGE"] = cardObjects["P1-BATTLEFIELD-PORO-FORGE"] with
        {
            OwnerId = "P2",
            ControllerId = ""
        };
        state = state with
        {
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-forge-dirty-control", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-PORO-FORGE",
                "LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD",
                ["P1-LEGEND-BASE-UNIT", "P1-PORO-FORGE-ARMAMENT"],
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.False(result.State.CardObjects["P1-LEGEND-PORO-FORGE"].IsExhausted);
        Assert.Null(result.State.CardObjects["P1-PORO-FORGE-ARMAMENT"].AttachedToObjectId);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task P79BattlefieldForgeLegendAttachRejectsLegendSourceWithoutCardNo()
    {
        var state = BattlefieldLegendAttachArmamentState(includeForge: true, armamentAttachedToObjectId: null);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-PORO-FORGE"] = cardObjects["P1-LEGEND-PORO-FORGE"] with
        {
            CardNo = null
        };
        state = state with
        {
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-battlefield-forge-legend-unknown-source", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-PORO-FORGE",
                "LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD",
                ["P1-LEGEND-BASE-UNIT", "P1-PORO-FORGE-ARMAMENT"],
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.False(result.State.CardObjects["P1-LEGEND-PORO-FORGE"].IsExhausted);
        Assert.Null(result.State.CardObjects["P1-PORO-FORGE-ARMAMENT"].AttachedToObjectId);
    }

    [Fact]
    public async Task P79LegendActDariusGainsManaAfterAnotherCardThisTurn()
    {
        var state = LegendActiveAbilityState("OGN·253/298", "P1-LEGEND-DARIUS", mana: 0) with
        {
            PlayerCardsPlayedThisTurn = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 1
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-darius-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-DARIUS",
                "LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA",
                [],
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.True(result.State.CardObjects["P1-LEGEND-DARIUS"].IsExhausted);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_EXHAUSTED", StringComparison.Ordinal));
        var manaEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.Equal(1, manaEvent.Payload["mana"]);
        Assert.Equal(1, manaEvent.Payload["manaAfter"]);
    }

    [Fact]
    public async Task P79LegendActDariusRequiresAnotherCardPlayedThisTurn()
    {
        var state = LegendActiveAbilityState("OGN·302/298", "P1-LEGEND-DARIUS-REPRINT", mana: 0);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-darius-legend-act-rejected", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-DARIUS-REPRINT",
                "LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA",
                [],
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.False(result.State.CardObjects["P1-LEGEND-DARIUS-REPRINT"].IsExhausted);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task P79LegendActDianaGainsManaDuringSpellDuelFocus()
    {
        var state = LegendSpellDuelFocusState("UNL-197/219", "P1-LEGEND-DIANA");
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.Equal(["LEGEND_ACT", "PASS_FOCUS", "SURRENDER"], prompt.EnabledActions());
        Assert.Contains(prompt.Candidates ?? [], candidate =>
            string.Equals(candidate.Action, "LEGEND_ACT", StringComparison.Ordinal)
            && candidate.Enabled);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-diana-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-DIANA",
                "LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA",
                [],
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(RunePool.Empty with { DuelOnlyMana = 1 }, result.State.RunePools["P1"]);
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P1", result.State.FocusPlayerId);
        Assert.True(result.State.CardObjects["P1-LEGEND-DIANA"].IsExhausted);
        var manaEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.Equal(1, manaEvent.Payload["mana"]);
        Assert.Equal(0, manaEvent.Payload["manaAfter"]);
        Assert.Equal(1, manaEvent.Payload["duelOnlyManaAfter"]);
        Assert.Equal("SPELL_DUEL", manaEvent.Payload["manaRestriction"]);
    }

    [Fact]
    public async Task P79LegendActDianaRejectsOutsideSpellDuelFocus()
    {
        var state = LegendActiveAbilityState("UNL-234/219", "P1-LEGEND-DIANA-REPRINT", mana: 0);

        // Retain this historical method and all original rejection assertions.
        // Fresh CN text allows neutral Reaction production; only consumption is
        // duel-restricted. Prove that control before testing the waiting player.
        var allowed = await new CoreRuleEngine().ResolveAsync(state,
            new("diana-neutral-source-control", "P1", CommandTypes.LegendAct),
            new LegendActCommand("P1-LEGEND-DIANA-REPRINT", LegendActionAbilityCatalog.DianaLegendAbilityId, [], []), default);
        Assert.True(allowed.Accepted, allowed.ErrorMessage);
        Assert.Equal(RunePool.Empty with { DuelOnlyMana = 1 }, allowed.State.RunePools["P1"]);
        state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-diana-legend-act-rejected", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-DIANA-REPRINT",
                "LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA",
                [],
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("当前时点不能使用该传奇行动。", result.ErrorMessage);
        Assert.DoesNotContain("LEGEND_ACT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.False(result.State.CardObjects["P1-LEGEND-DIANA-REPRINT"].IsExhausted);
    }

    [Fact]
    public async Task P79LegendActKaisaGainsPowerInPriorityWindowForPendingSpell()
    {
        var state = LegendPriorityWindowState("OGN·247/298", "P1-LEGEND-KAISA", CardObjectTags.SpellCard);
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.Equal(["LEGEND_ACT", "PASS_PRIORITY", "SURRENDER"], prompt.EnabledActions());
        Assert.Contains(prompt.Candidates ?? [], candidate =>
            string.Equals(candidate.Action, "LEGEND_ACT", StringComparison.Ordinal)
            && candidate.Enabled);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-kaisa-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-KAISA",
                "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL",
                [],
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(RunePool.Empty with { SpellOnlyPower = 1 }, result.State.RunePools["P1"]);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.True(result.State.CardObjects["P1-LEGEND-KAISA"].IsExhausted);
        var powerEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(1, powerEvent.Payload["power"]);
        Assert.Equal(0, powerEvent.Payload["powerAfter"]);
        Assert.Equal(1, powerEvent.Payload["spellOnlyPowerAfter"]);
        Assert.Equal("PLAY_SPELL", powerEvent.Payload["powerPurpose"]);
    }

    [Fact]
    public async Task P79LegendActKaisaRequiresPendingSpellStackItem()
    {
        var state = LegendPriorityWindowState("OGN·299/298", "P1-LEGEND-KAISA-REPRINT", CardObjectTags.EquipmentCard);

        // Resource production need not match the already-paid stack object.
        // Preserve that old fixture as a positive bank, then retain the original
        // InvalidTarget/no-cost assertions against the opponent's stack source.
        var allowed = await new CoreRuleEngine().ResolveAsync(state,
            new("kaisa-nonmatching-stack-resource-control", "P1", CommandTypes.LegendAct),
            new LegendActCommand("P1-LEGEND-KAISA-REPRINT", LegendActionAbilityCatalog.KaisaLegendAbilityId, [], []), default);
        Assert.True(allowed.Accepted, allowed.ErrorMessage);
        Assert.Equal(RunePool.Empty with { SpellOnlyPower = 1 }, allowed.State.RunePools["P1"]);
        Assert.Equal(state.PriorityPlayerId, allowed.State.PriorityPlayerId);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-kaisa-legend-act-rejected", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P2-PENDING-SOURCE",
                "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL",
                [],
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.False(result.State.CardObjects["P1-LEGEND-KAISA-REPRINT"].IsExhausted);
    }

    [Fact]
    public async Task P79LegendActOrnnGainsPowerInPriorityWindowForPendingEquipment()
    {
        var state = LegendPriorityWindowState("SFD·189/221", "P1-LEGEND-ORNN", CardObjectTags.EquipmentCard);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-ornn-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-ORNN",
                "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT",
                [],
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(RunePool.Empty with { EquipmentOnlyPower = 1 }, result.State.RunePools["P1"]);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.True(result.State.CardObjects["P1-LEGEND-ORNN"].IsExhausted);
        var powerEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(1, powerEvent.Payload["power"]);
        Assert.Equal(0, powerEvent.Payload["powerAfter"]);
        Assert.Equal(1, powerEvent.Payload["equipmentOnlyPowerAfter"]);
        Assert.Equal("EQUIPMENT", powerEvent.Payload["powerPurpose"]);
    }

    [Fact]
    public async Task P79LegendActTeemoRecallsOwnedChampionZoneTeemoUnit()
    {
        var state = TeemoLegendRecallState("OGN·263/298", "P1-LEGEND-TEEMO", mana: 1);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-teemo-recall-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-TEEMO",
                "LEGEND_PAY_1_EXHAUST_RECALL_OWNED_TEEMO_UNIT",
                ["P1-TEEMO-UNIT"],
                ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.True(result.State.CardObjects["P1-LEGEND-TEEMO"].IsExhausted);
        Assert.DoesNotContain("P1-TEEMO-UNIT", result.State.PlayerZones["P1"].ChampionZone);
        Assert.Contains("P1-TEEMO-UNIT", result.State.PlayerZones["P1"].Hand);
        var returnedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_RETURNED_TO_HAND", StringComparison.Ordinal));
        Assert.Equal("CHAMPION", returnedEvent.Payload["originZone"]);
        Assert.Equal("P1-TEEMO-UNIT", returnedEvent.Payload["targetObjectId"]);
    }

    [Fact]
    public async Task P79LegendTeemoStandbyReplacementHidesStandbyCardWithMana()
    {
        var state = TeemoLegendStandbyHideState(hasTeemoLegend: true);

        state = StandbyTestDriver.WithControlledBattlefield(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-teemo-standby-hide", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-STANDBY-TEEMO",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_TEEMO_MANA"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.DoesNotContain("P1-STANDBY-TEEMO", result.State.PlayerZones["P1"].Hand);
        Assert.Contains("P1-STANDBY-TEEMO", result.State.PlayerZones["P1"].Battlefields);
        Assert.True(result.State.CardObjects["P1-STANDBY-TEEMO"].IsFaceDown);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(true, costEvent.Payload["teemoStandbyHideReplacement"]);
    }

    [Fact]
    public async Task P79LegendTeemoStandbyReplacementRequiresTeemoLegend()
    {
        var state = TeemoLegendStandbyHideState(hasTeemoLegend: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-teemo-standby-hide-rejected", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-STANDBY-TEEMO",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_TEEMO_MANA"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.Contains("P1-STANDBY-TEEMO", result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("P1-STANDBY-TEEMO", result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task P79LegendActAzirCreatesTemperedSandSoldierAfterArmamentPlayedThisTurn()
    {
        var state = LegendActiveAbilityState("SFD·197/221", "P1-LEGEND-AZIR", mana: 1) with
        {
            UntilEndOfTurnEffects = ["PLAYED_ARMAMENT_THIS_TURN:P1"]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-azir-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-AZIR",
                "LEGEND_PAY_1_EXHAUST_CREATE_SAND_SOLDIER_AFTER_ARMAMENT",
                [],
                ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Single(result.State.StackItems);
        Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_TOKEN_CREATED");
        var activationEvents = result.Events;
        result = await OfficialGraveyardRecastTests.Top(result.State);
        result = result with { Events = activationEvents.Concat(result.Events).ToArray() };
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.True(result.State.CardObjects["P1-LEGEND-AZIR"].IsExhausted);
        Assert.Contains("P1-LEGEND-AZIR-TOKEN-001", result.State.PlayerZones["P1"].Base);
        var token = result.State.CardObjects["P1-LEGEND-AZIR-TOKEN-001"];
        Assert.Equal("SFD·T02", token.CardNo);
        Assert.Equal(2, token.Power);
        Assert.True(token.IsExhausted);
        Assert.Equal("P1", token.OwnerId);
        Assert.Equal("P1", token.ControllerId);
        Assert.Contains(CardObjectTags.UnitCard, token.Tags);
        Assert.Contains(CardObjectTags.SandSoldier, token.Tags);
        Assert.Contains(CardEquipmentKeywordNames.Tempered, token.Tags);
        var tokenEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Equal("SFD·T02", tokenEvent.Payload["tokenCardNo"]);
        Assert.Equal(true, tokenEvent.Payload["azirTempered"]);
    }

    [Fact]
    public async Task P79LegendActAzirRequiresArmamentPlayedThisTurn()
    {
        var state = LegendActiveAbilityState("SFD·247/221", "P1-LEGEND-AZIR-REPRINT", mana: 1);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-azir-legend-act-rejected", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-AZIR-REPRINT",
                "LEGEND_PAY_1_EXHAUST_CREATE_SAND_SOLDIER_AFTER_ARMAMENT",
                [],
                ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.False(result.State.CardObjects["P1-LEGEND-AZIR-REPRINT"].IsExhausted);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task P79LegendAzirTracksArmamentPlayedThisTurnFromWeaponPlay()
    {
        var baseState = LegendActiveAbilityState("SFD·197/221", "P1-LEGEND-AZIR", mana: 2);
        var playerZones = baseState.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Hand = ["P1-DORANS-BLADE"]
        };
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-DORANS-BLADE"] = new(
            "P1-DORANS-BLADE",
            cardNo: "SFD·095/221",
            tags: [CardObjectTags.EquipmentCard],
            ownerId: "P1",
            controllerId: "P1");
        var state = baseState with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-azir-armament-play", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-DORANS-BLADE", "SFD·095/221", []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Contains("PLAYED_ARMAMENT_THIS_TURN:P1", result.State.UntilEndOfTurnEffects);
        Assert.DoesNotContain("P1-DORANS-BLADE", result.State.PlayerZones["P1"].Hand);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendTriggerRengarGivesUnitPlusOneAfterUnitPlayed()
    {
        var state = RengarLegendUnitPlayState("UNL-183/219", "P1-LEGEND-RENGAR");

        var playResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-rengar-play-unit", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-UNIT-PLUCKY-PORO", "UNL-220/219", ["P1-LEGEND-BASE-UNIT"]),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        var stackItem = Assert.Single(playResult.State.StackItems);
        Assert.Equal(["P1-LEGEND-BASE-UNIT"], stackItem.TargetObjectIds);
        Assert.Equal("P1-LEGEND-RENGAR", stackItem.SourceObjectId);
        Assert.NotNull(stackItem.UnitPlayMight);
        Assert.Contains(playResult.State.UntilEndOfTurnEffects, effectId =>
            effectId.StartsWith("RENGAR_UNIT_PLAYED_TARGET:", StringComparison.Ordinal)
            && effectId.EndsWith(":P1-LEGEND-BASE-UNIT", StringComparison.Ordinal));

        var p1Pass = await new CoreRuleEngine().ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-p7-9-rengar-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await new CoreRuleEngine().ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-rengar-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted);
        Assert.Contains("P1-UNIT-PLUCKY-PORO", p2Pass.State.PlayerZones["P1"].Base);
        var target = p2Pass.State.CardObjects["P1-LEGEND-BASE-UNIT"];
        Assert.Equal(3, target.Power);
        Assert.Equal(1, target.UntilEndOfTurnPowerModifier);
        var modifier = Assert.Single(target.UntilEndOfTurnPowerModifiers);
        Assert.Equal(1, modifier.PowerDelta);
        Assert.Equal(1, modifier.RequestedPowerDelta);
        Assert.Equal(0, modifier.MinimumPower);
        Assert.Equal(3, modifier.ResultingPower);
        Assert.Equal(2, modifier.BasePower);
        Assert.Equal(3, modifier.EffectivePower);
        Assert.Equal(1, modifier.AppliedOrder);
        Assert.Equal("P1-LEGEND-RENGAR", modifier.SourceObjectId);
        Assert.Equal("UNL-183/219", modifier.SourceCardNo);
        Assert.Equal("UNIT_PLAYED_POWER_PLUS_1", modifier.EffectKind);
        Assert.Equal("CoreRuleEngine.ResolveRengarUnitPlayedPowerTrigger", modifier.SourcePath);
        var powerEffect = Assert.Single(
            p2Pass.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, "P1-LEGEND-BASE-UNIT", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-RENGAR", powerEffect.SourceObjectId);
        Assert.Equal("UNL-183/219", powerEffect.SourceCardNo);
        Assert.Equal("UNIT_PLAYED_POWER_PLUS_1", powerEffect.EffectKind);
        Assert.Equal("CoreRuleEngine.ResolveRengarUnitPlayedPowerTrigger", powerEffect.SourcePath);
        Assert.Equal(1, powerEffect.RequestedPowerDelta);
        Assert.Equal(1, powerEffect.AppliedPowerDelta);
        Assert.Equal(0, powerEffect.MinimumPower);
        Assert.Equal(3, powerEffect.ResultingPower);
        Assert.Equal(1, powerEffect.AppliedOrder);
        var continuousEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            p2Pass.Snapshots["P1"].Timing["continuousEffects"]);
        var powerEffectView = Assert.Single(
            continuousEffects,
            effect => string.Equals(effect["layer"] as string, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal)
                && string.Equals(effect["targetObjectId"] as string, "P1-LEGEND-BASE-UNIT", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-RENGAR", powerEffectView["sourceObjectId"]);
        Assert.Equal("UNL-183/219", powerEffectView["sourceCardNo"]);
        Assert.Equal("UNIT_PLAYED_POWER_PLUS_1", powerEffectView["effectKind"]);
        Assert.Equal("CoreRuleEngine.ResolveRengarUnitPlayedPowerTrigger", powerEffectView["sourcePath"]);
        Assert.Equal(1, Assert.IsType<int>(powerEffectView["requestedPowerDelta"]));
        Assert.Equal(1, Assert.IsType<int>(powerEffectView["appliedPowerDelta"]));
        Assert.Equal(0, Assert.IsType<int>(powerEffectView["minimumPower"]));
        Assert.Equal(3, Assert.IsType<int>(powerEffectView["resultingPower"]));
        Assert.Equal(1, Assert.IsType<int>(powerEffectView["appliedOrder"]));
        var ended = await new CoreRuleEngine().ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-p7-9-rengar-end-turn-after-power-ledger", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(ended.Accepted, ended.ErrorMessage);
        var cleanedTarget = ended.State.CardObjects["P1-LEGEND-BASE-UNIT"];
        Assert.Equal(2, cleanedTarget.Power);
        Assert.Equal(0, cleanedTarget.UntilEndOfTurnPowerModifier);
        Assert.Empty(cleanedTarget.UntilEndOfTurnPowerModifiers);
        Assert.DoesNotContain(
            ended.State.ContinuousEffects,
            effect => string.Equals(effect.TargetObjectId, "P1-LEGEND-BASE-UNIT", StringComparison.Ordinal)
                && string.Equals(effect.Layer, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal));
        var cleanedContinuousEffects = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(
            ended.Snapshots["P1"].Timing["continuousEffects"]);
        Assert.DoesNotContain(
            cleanedContinuousEffects,
            effect => string.Equals(effect["targetObjectId"] as string, "P1-LEGEND-BASE-UNIT", StringComparison.Ordinal)
                && string.Equals(effect["layer"] as string, ContinuousEffectLayers.PowerModifier, StringComparison.Ordinal));
        var triggerEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "UNIT_PLAYED_POWER_PLUS_1", StringComparison.Ordinal));
        Assert.Equal("UNL-183/219", triggerEvent.Payload["legendCardNo"]);
        var powerEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "UNIT_PLAYED_POWER_PLUS_1", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-BASE-UNIT", powerEvent.Payload["targetObjectId"]);
    }

    [Fact]
    public async Task P79LegendTriggerRengarRejectsNonUnitTriggerTarget()
    {
        var state = RengarLegendUnitPlayState("UNL-227*/219", "P1-LEGEND-RENGAR-REPRINT");
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Base = playerZones["P1"].Base.Concat(["P1-RENGAR-EQUIPMENT"]).ToArray()
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-RENGAR-EQUIPMENT"] = new(
            "P1-RENGAR-EQUIPMENT",
            tags: [CardObjectTags.EquipmentCard],
            ownerId: "P1",
            controllerId: "P1");
        state = state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-rengar-invalid-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-UNIT-PLUCKY-PORO", "UNL-220/219", ["P1-RENGAR-EQUIPMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("雷恩加尔传奇触发目标必须是场上的单位或本次打出的单位。", result.ErrorMessage);
        Assert.DoesNotContain("Rengar legend trigger", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("PLAY_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("P1-UNIT-PLUCKY-PORO", result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P79LegendTriggerRengarRejectsOpponentControlledFieldTriggerTarget()
    {
        var state = RengarLegendUnitPlayState("UNL-227/219", "P1-LEGEND-RENGAR-ALT");
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P2"] = playerZones["P2"] with
        {
            Base = ["P2-DIRTY-P1-CONTROLLED-RENGAR-TARGET"]
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P2-DIRTY-P1-CONTROLLED-RENGAR-TARGET"] = new(
            "P2-DIRTY-P1-CONTROLLED-RENGAR-TARGET",
            cardNo: "SFD·125/221",
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        state = state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            PriorityPlayerId = null,
            StackItems = []
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-rengar-dirty-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-UNIT-PLUCKY-PORO", "UNL-220/219", ["P2-DIRTY-P1-CONTROLLED-RENGAR-TARGET"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("雷恩加尔传奇触发目标必须是场上的单位或本次打出的单位。", result.ErrorMessage);
        Assert.DoesNotContain("Rengar legend trigger", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("PLAY_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("P1-UNIT-PLUCKY-PORO", result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P79LegendTriggerRengarSkipsPowerWhenStoredTargetControlChanges()
    {
        var state = RengarLegendUnitPlayState("UNL-183/219", "P1-LEGEND-RENGAR");

        var playResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-rengar-play-unit-dirty-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-UNIT-PLUCKY-PORO", "UNL-220/219", ["P1-LEGEND-BASE-UNIT"]),
            CancellationToken.None);

        Assert.True(playResult.Accepted);

        var cardObjects = playResult.State.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-BASE-UNIT"] = cardObjects["P1-LEGEND-BASE-UNIT"] with
        {
            ControllerId = "P2"
        };
        var dirtyControlState = playResult.State with { CardObjects = cardObjects };

        var p1Pass = await new CoreRuleEngine().ResolveAsync(
            dirtyControlState,
            new PlayerIntent("intent-p7-9-rengar-dirty-target-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await new CoreRuleEngine().ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-rengar-dirty-target-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted);
        Assert.Contains("P1-UNIT-PLUCKY-PORO", p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "UNIT_PLAYED_POWER_PLUS_1", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "UNIT_PLAYED_POWER_PLUS_1", StringComparison.Ordinal));
        var target = p2Pass.State.CardObjects["P1-LEGEND-BASE-UNIT"];
        Assert.Equal("P2", target.ControllerId);
        Assert.Equal(2, target.Power);
        Assert.Equal(0, target.UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task P79LegendTriggerRengarSkipsPowerWhenLegendBecomesOpponentOwned()
    {
        var state = RengarLegendUnitPlayState("UNL-183/219", "P1-LEGEND-RENGAR");

        var playResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-rengar-play-unit-dirty-legend", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-UNIT-PLUCKY-PORO", "UNL-220/219", ["P1-LEGEND-BASE-UNIT"]),
            CancellationToken.None);

        Assert.True(playResult.Accepted);

        var cardObjects = playResult.State.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-RENGAR"] = cardObjects["P1-LEGEND-RENGAR"] with
        {
            OwnerId = "P2",
            ControllerId = ""
        };
        var dirtyLegendState = playResult.State with { CardObjects = cardObjects };

        var p1Pass = await new CoreRuleEngine().ResolveAsync(
            dirtyLegendState,
            new PlayerIntent("intent-p7-9-rengar-dirty-legend-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await new CoreRuleEngine().ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-rengar-dirty-legend-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "UNIT_PLAYED_POWER_PLUS_1", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "UNIT_PLAYED_POWER_PLUS_1", StringComparison.Ordinal));
        var target = p2Pass.State.CardObjects["P1-LEGEND-BASE-UNIT"];
        Assert.Equal(2, target.Power);
        Assert.Equal(0, target.UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task P79LegendTriggerLeonaGrantsBoonWhenEnemyStunned()
    {
        var state = LeonaLegendStunState("OGN·261/298", "P1-LEGEND-LEONA");

        var playResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-leona-play-stunner", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SUN-SHIELDGUARD",
                "OGN·051/298",
                ["P2-LEONA-STUN-TARGET", "P1-LEGEND-BASE-UNIT"]),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        var stackItem = Assert.Single(playResult.State.StackItems);
        Assert.Equal(["P2-LEONA-STUN-TARGET"], stackItem.TargetObjectIds);
        Assert.Contains(playResult.State.UntilEndOfTurnEffects, effectId =>
            effectId.StartsWith("LEONA_STUN_BOON_TARGET:", StringComparison.Ordinal)
            && effectId.EndsWith(":P1-LEGEND-BASE-UNIT", StringComparison.Ordinal));

        var p1Pass = await new CoreRuleEngine().ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-p7-9-leona-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await new CoreRuleEngine().ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-leona-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted);
        Assert.Contains("P1-UNIT-SUN-SHIELDGUARD", p2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains("STUNNED", p2Pass.State.CardObjects["P2-LEONA-STUN-TARGET"].UntilEndOfTurnEffects);
        var boonTarget = p2Pass.State.CardObjects["P1-LEGEND-BASE-UNIT"];
        Assert.Equal(3, boonTarget.Power);
        Assert.Contains(CardObjectTags.Boon, boonTarget.Tags);
        var triggerEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "ENEMY_STUNNED_GRANT_BOON", StringComparison.Ordinal));
        Assert.Equal("OGN·261/298", triggerEvent.Payload["legendCardNo"]);
        var boonEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal));
        Assert.Equal("P1-LEGEND-BASE-UNIT", boonEvent.Payload["targetObjectId"]);
        Assert.Equal(false, boonEvent.Payload["alreadyHadBoon"]);
    }

    [Fact]
    public async Task P79LegendTriggerLeonaSkipsBoonWhenStoredTargetControlChanges()
    {
        var state = LeonaLegendStunState("OGN·261/298", "P1-LEGEND-LEONA");

        var playResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-leona-play-stunner-dirty-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SUN-SHIELDGUARD",
                "OGN·051/298",
                ["P2-LEONA-STUN-TARGET", "P1-LEGEND-BASE-UNIT"]),
            CancellationToken.None);

        Assert.True(playResult.Accepted);

        var cardObjects = playResult.State.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-BASE-UNIT"] = cardObjects["P1-LEGEND-BASE-UNIT"] with
        {
            ControllerId = "P2"
        };
        var dirtyControlState = playResult.State with { CardObjects = cardObjects };

        var p1Pass = await new CoreRuleEngine().ResolveAsync(
            dirtyControlState,
            new PlayerIntent("intent-p7-9-leona-dirty-target-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await new CoreRuleEngine().ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-leona-dirty-target-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "ENEMY_STUNNED_GRANT_BOON", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BOON_GRANTED", StringComparison.Ordinal));
        var boonTarget = p2Pass.State.CardObjects["P1-LEGEND-BASE-UNIT"];
        Assert.Equal("P2", boonTarget.ControllerId);
        Assert.DoesNotContain(CardObjectTags.Boon, boonTarget.Tags);
    }

    [Fact]
    public async Task P79LegendTriggerLeonaRejectsNonFriendlyUnitBoonTarget()
    {
        var state = LeonaLegendStunState("OGN·306*/298", "P1-LEGEND-LEONA-REPRINT");
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Base = playerZones["P1"].Base.Concat(["P1-LEONA-EQUIPMENT"]).ToArray()
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEONA-EQUIPMENT"] = new(
            "P1-LEONA-EQUIPMENT",
            tags: [CardObjectTags.EquipmentCard],
            ownerId: "P1",
            controllerId: "P1");
        state = state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-leona-invalid-boon-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SUN-SHIELDGUARD",
                "OGN·051/298",
                ["P2-LEONA-STUN-TARGET", "P1-LEONA-EQUIPMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("蕾欧娜传奇触发目标必须是己方场上的单位或本次打出的单位。", result.ErrorMessage);
        Assert.DoesNotContain("Leona legend trigger", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("PLAY_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("P1-UNIT-SUN-SHIELDGUARD", result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P79LegendTriggerSivirCreatesDormantGoldWhenControllerRecyclesRune()
    {
        var state = SivirRuneRecycleState("SFD·203/221", "P1-LEGEND-SIVIR");

        var playResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-sivir-play-scrying-shell", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SCRYING-SHELL", "UNL-161/219", []),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        var p1Pass = await new CoreRuleEngine().ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-p7-9-sivir-rune-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await new CoreRuleEngine().ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-sivir-rune-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted);
        var predict=Assert.IsType<PendingCardChoiceState>(p2Pass.State.PendingCardChoice);
        Assert.Equal("INSIGHT",predict.ChoiceWindow);Assert.Equal(["P1-SIVIR-RUNE-RECYCLE"],predict.LegalObjectIds);
        p2Pass=await new CoreRuleEngine().ResolveAsync(p2Pass.State,new PlayerIntent("sivir-predict-choose","P1",CommandTypes.ChooseCards),
            new ChooseCardsCommand(predict.ChoiceId,predict.ChoiceWindow,["P1-SIVIR-RUNE-RECYCLE"]),CancellationToken.None);
        Assert.True(p2Pass.Accepted,p2Pass.ErrorMessage);
        var recycledEvents = p2Pass.Events;
        Assert.False(p2Pass.State.CardObjects["P1-LEGEND-SIVIR"].IsExhausted);
        var optional = Assert.IsType<PendingCardChoiceState>(p2Pass.State.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", optional.ChoiceWindow);
        var paid = await OfficialGraveyardRecastTests.Act(p2Pass.State, "P1",
            new ChooseCardsCommand(optional.ChoiceId, optional.ChoiceWindow, ["P1-LEGEND-SIVIR"]));
        Assert.DoesNotContain(paid.Events, e => e.Kind == "EQUIPMENT_TOKEN_CREATED");
        var resolved = await OfficialGraveyardRecastTests.Top(paid.State);
        p2Pass = resolved with { Events = recycledEvents.Concat(paid.Events).Concat(resolved.Events).ToArray() };
        Assert.True(p2Pass.State.CardObjects["P1-LEGEND-SIVIR"].IsExhausted);
        Assert.All(p2Pass.Events.Where(e => e.Kind == "CARDS_RECYCLED"), e => Assert.False(e.Payload.ContainsKey("cardIds")));
        Assert.Contains("P1-LEGEND-SIVIR-TOKEN-001", p2Pass.State.PlayerZones["P1"].Base);
        var token = p2Pass.State.CardObjects["P1-LEGEND-SIVIR-TOKEN-001"];
        Assert.True(token.IsExhausted);
        Assert.Contains(CardObjectTags.EquipmentCard, token.Tags);
        var triggerEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "RUNE_RECYCLED_CREATE_GOLD", StringComparison.Ordinal));
        Assert.Equal("SFD·203/221", triggerEvent.Payload["legendCardNo"]);
        var tokenEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Equal("金币", tokenEvent.Payload["tokenName"]);
        Assert.Equal(true, tokenEvent.Payload["isExhausted"]);
    }

    [Fact]
    public async Task P79LegendTriggerSivirSkipsGoldWhenLegendBecomesOpponentOwned()
    {
        var state = SivirRuneRecycleState("SFD·203/221", "P1-LEGEND-SIVIR");

        var playResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-sivir-play-scrying-shell-dirty-legend", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SCRYING-SHELL", "UNL-161/219", []),
            CancellationToken.None);

        Assert.True(playResult.Accepted);

        var cardObjects = playResult.State.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-SIVIR"] = cardObjects["P1-LEGEND-SIVIR"] with
        {
            OwnerId = "P2",
            ControllerId = ""
        };
        var dirtyLegendState = playResult.State with { CardObjects = cardObjects };

        var p1Pass = await new CoreRuleEngine().ResolveAsync(
            dirtyLegendState,
            new PlayerIntent("intent-p7-9-sivir-dirty-legend-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await new CoreRuleEngine().ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-sivir-dirty-legend-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted);
        var predict=Assert.IsType<PendingCardChoiceState>(p2Pass.State.PendingCardChoice);
        Assert.Equal("INSIGHT",predict.ChoiceWindow);Assert.Equal(["P1-SIVIR-RUNE-RECYCLE"],predict.LegalObjectIds);
        p2Pass=await new CoreRuleEngine().ResolveAsync(p2Pass.State,new PlayerIntent("sivir-predict-choose","P1",CommandTypes.ChooseCards),
            new ChooseCardsCommand(predict.ChoiceId,predict.ChoiceWindow,["P1-SIVIR-RUNE-RECYCLE"]),CancellationToken.None);
        Assert.True(p2Pass.Accepted,p2Pass.ErrorMessage);
        Assert.False(p2Pass.State.CardObjects["P1-LEGEND-SIVIR"].IsExhausted);
        Assert.DoesNotContain("P1-LEGEND-SIVIR-TOKEN-001", p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "RUNE_RECYCLED_CREATE_GOLD", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_EXHAUSTED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendTriggerSivirReadiesWhenEnemyUnitDestroyed()
    {
        var state = SivirEnemyDestroyState("SFD·250/221", "P1-LEGEND-SIVIR-REPRINT");
        state = PrintedCostFixture.Add(state, "P1", "UNL-159/219");

        var playResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-sivir-play-hunt", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-HUNT-THE-WEAK", "UNL-159/219", ["P2-SIVIR-DESTROY-TARGET"]),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        var p1Pass = await new CoreRuleEngine().ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-p7-9-sivir-destroy-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await new CoreRuleEngine().ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-sivir-destroy-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted);
        Assert.False(p2Pass.State.CardObjects["P1-LEGEND-SIVIR-REPRINT"].IsExhausted);
        Assert.Equal(["P2-SIVIR-DESTROY-TARGET"], p2Pass.State.PlayerZones["P2"].Graveyard);
        var triggerEvent = Assert.Single(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "ENEMY_UNIT_DESTROYED_READY", StringComparison.Ordinal));
        Assert.Equal("SFD·250/221", triggerEvent.Payload["legendCardNo"]);
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_READIED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendTriggerSivirReadiesWhenOpponentDestroysOwnUnit()
    {
        var state = SivirOpponentSelfDestroyState("SFD·203/221", "P1-LEGEND-SIVIR");
        state = PrintedCostFixture.Add(state, "P2", "UNL-159/219");

        var playResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-sivir-p2-play-hunt", "P2", "PLAY_CARD"),
            new PlayCardCommand("P2-SPELL-HUNT-THE-WEAK", "UNL-159/219", ["P2-SIVIR-SELF-DESTROY-TARGET"]),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        var p2Pass = await new CoreRuleEngine().ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-p7-9-sivir-self-destroy-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p1Pass = await new CoreRuleEngine().ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-p7-9-sivir-self-destroy-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.False(p1Pass.State.CardObjects["P1-LEGEND-SIVIR"].IsExhausted);
        Assert.Contains("P2-SIVIR-SELF-DESTROY-TARGET", p1Pass.State.PlayerZones["P2"].Graveyard);
        var triggerEvent = Assert.Single(p1Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "ENEMY_UNIT_DESTROYED_READY", StringComparison.Ordinal));
        Assert.Equal("P1", triggerEvent.Payload["playerId"]);
        Assert.Equal("SFD·203/221", triggerEvent.Payload["legendCardNo"]);
        Assert.Contains(p1Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_READIED", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("UNL-181/219", "P1-LEGEND-JHIN")]
    [InlineData("UNL-226/219", "P1-LEGEND-JHIN-ALT")]
    [InlineData("UNL-226*/219", "P1-LEGEND-JHIN-PREMIUM")]
    public async Task P79LegendTriggerJhinCompletesFourthBanishedHighCostSpell(string sourceCardNo, string sourceObjectId)
    {
        var state = JhinHighCostSpellCompletionState(sourceCardNo, sourceObjectId);
        state = state with { LinkedExiles = [new(sourceObjectId, state.CardObjects[sourceObjectId].ObjectGeneration, "P1", state.PlayerZones["P1"].Banished.ToDictionary(id=>id,id=>state.CardObjects[id].ObjectGeneration))] };
        state = PrintedCostFixture.Add(state, "P1", "UNL-180/219");

        var playResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-p7-9-jhin-{sourceCardNo}-play-ruination", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-JHIN-RUINATION", "UNL-180/219", []),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        var p1Pass = await new CoreRuleEngine().ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-p7-9-jhin-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await new CoreRuleEngine().ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-jhin-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted);
        Assert.NotNull(p2Pass.State.PendingCardChoice);
        p2Pass = await SpellResolutionTestDriver.Finish(p2Pass);
        var p1Zones = p2Pass.State.PlayerZones["P1"];
        Assert.Contains("P1-JHIN-RUINATION", p1Zones.Graveyard);
        Assert.Contains("P1-JHIN-BANISHED-001", p1Zones.Graveyard);
        Assert.Contains("P1-JHIN-BANISHED-002", p1Zones.Graveyard);
        Assert.Contains("P1-JHIN-BANISHED-003", p1Zones.Graveyard);
        Assert.DoesNotContain("P1-JHIN-RUINATION", p1Zones.Banished);
        Assert.Contains("P1-JHIN-RUNE-001", p1Zones.Base);
        Assert.Contains("P1-JHIN-RUNE-004", p1Zones.Base);
        Assert.Contains("P1-JHIN-DRAW-001", p1Zones.Hand);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, TriggerKinds.LegendHighCostSpellBanishCompletion, StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, TriggerKinds.LegendHighCostSpellBanishCompletion, StringComparison.Ordinal));
        var runeEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.Equal(4, runeEvent.Payload["count"]);
        var drawEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal(1, drawEvent.Payload["count"]);
    }

    [Fact]
    public async Task P79LegendTriggerJhinSkipsHighCostSpellWhenLegendBecomesOpponentOwned()
    {
        var state = JhinHighCostSpellCompletionState("UNL-181/219", "P1-LEGEND-JHIN");
        state = PrintedCostFixture.Add(state, "P1", "UNL-180/219");

        var playResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-jhin-play-ruination-dirty-legend", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-JHIN-RUINATION", "UNL-180/219", []),
            CancellationToken.None);

        Assert.True(playResult.Accepted);

        var cardObjects = playResult.State.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-JHIN"] = cardObjects["P1-LEGEND-JHIN"] with
        {
            OwnerId = "P2",
            ControllerId = ""
        };
        var dirtyLegendState = playResult.State with { CardObjects = cardObjects };

        var p1Pass = await new CoreRuleEngine().ResolveAsync(
            dirtyLegendState,
            new PlayerIntent("intent-p7-9-jhin-dirty-legend-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await new CoreRuleEngine().ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-p7-9-jhin-dirty-legend-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted);
        var p1Zones = p2Pass.State.PlayerZones["P1"];
        Assert.Contains("P1-JHIN-RUINATION", p1Zones.Graveyard);
        Assert.DoesNotContain("P1-JHIN-RUINATION", p1Zones.Banished);
        Assert.DoesNotContain("P1-JHIN-DRAW-001", p1Zones.Hand);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && (string.Equals(gameEvent.Payload["trigger"] as string, "HIGH_COST_SPELL_BANISHED", StringComparison.Ordinal)
                || string.Equals(gameEvent.Payload["trigger"] as string, "FOUR_HIGH_COST_SPELLS_COMPLETED", StringComparison.Ordinal)));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNES_CALLED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendActEzrealDrawsAfterSecondEnemyTargetThisTurn()
    {
        var state = EzrealEnemyTargetSpellState("SFD·199/221", "P1-LEGEND-EZREAL", priorTargetCount: 1);
        state = PrintedCostFixture.Add(state, "P1", "UNL-159/219");

        var playResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-ezreal-play-second-target-spell", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-EZREAL-HUNT-002", "UNL-159/219", ["P2-EZREAL-TARGET-002"]),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        Assert.Contains("EZREAL_ENEMY_TARGETS_THIS_TURN:P1:2", playResult.State.UntilEndOfTurnEffects);
        var prompt = ResolutionResult.BuildPrompts(playResult.State)["P1"];
        Assert.Equal(["LEGEND_ACT", "PASS_PRIORITY", "SURRENDER"], prompt.EnabledActions());
        Assert.Contains(prompt.Candidates ?? [], candidate =>
            string.Equals(candidate.Action, "LEGEND_ACT", StringComparison.Ordinal)
            && candidate.Enabled
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, "P1-LEGEND-EZREAL", StringComparison.Ordinal)));

        var result = await new CoreRuleEngine().ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-p7-9-ezreal-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-EZREAL",
                "LEGEND_REACTION_EXHAUST_DRAW_AFTER_TWO_ENEMY_TARGETS",
                [],
                []),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(result.State.CardObjects["P1-LEGEND-EZREAL"].IsExhausted);
        Assert.Contains("P1-EZREAL-DRAW-001", result.State.PlayerZones["P1"].Hand);
        Assert.Single(result.State.StackItems);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_ABILITY_ACTIVATED", StringComparison.Ordinal));
        var drawEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal(1, drawEvent.Payload["count"]);
    }

    [Fact]
    public async Task P79LegendActEzrealRequiresTwoEnemyTargetsThisTurn()
    {
        var state = EzrealPriorityState("SFD·248/221", "P1-LEGEND-EZREAL-REPRINT", targetCount: 1);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-ezreal-legend-act-rejected", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-EZREAL-REPRINT",
                "LEGEND_REACTION_EXHAUST_DRAW_AFTER_TWO_ENEMY_TARGETS",
                [],
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.False(result.State.CardObjects["P1-LEGEND-EZREAL-REPRINT"].IsExhausted);
        Assert.DoesNotContain("P1-EZREAL-DRAW-001", result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task P79LegendTriggerJinxDrawsAtTurnStartWhenHandBelowTwo()
    {
        var state = JinxLegendTurnStartState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-jinx-end-turn", "P2", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Single(result.State.StackItems);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        result = await TurnSequenceTestDriver.Complete(result);
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchStatuses.InProgress, result.State.Status);
        Assert.Empty(result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-JINX-DRAW-001", "P1-JINX-DRAW-002"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal("HAND", result.State.ObjectLocations["P1-JINX-DRAW-001"].Zone);
        Assert.Equal("HAND", result.State.ObjectLocations["P1-JINX-DRAW-002"].Zone);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.Equal(2, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal)));
        Assert.Null(result.State.WinnerPlayerId);
    }

    [Fact]
    public async Task P79LegendActLilliaCreatesFaerieWithEphemeralCostReduction()
    {
        var state = LegendActiveAbilityWithEphemeralSupportState("UNL-189/219", "P1-LEGEND-LILLIA", mana: 3);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-lillia-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-LILLIA",
                "LEGEND_DYNAMIC_PAY_EXHAUST_CREATE_FAERIE",
                [],
                ["SPEND_MANA:3"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Single(result.State.StackItems);
        Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_TOKEN_CREATED");
        var activationEvents = result.Events;
        result = await OfficialGraveyardRecastTests.Top(result.State);
        result = result with { Events = activationEvents.Concat(result.Events).ToArray() };
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.True(result.State.CardObjects["P1-LEGEND-LILLIA"].IsExhausted);
        Assert.Contains("P1-LEGEND-LILLIA-TOKEN-001", result.State.PlayerZones["P1"].Base);
        var token = result.State.CardObjects["P1-LEGEND-LILLIA-TOKEN-001"];
        Assert.Equal("UNL·T07", token.CardNo);
        Assert.Equal(3, token.Power);
        Assert.False(token.IsExhausted);
        Assert.Contains(CardObjectTags.UnitCard, token.Tags);
        Assert.Contains(CardObjectTags.Ephemeral, token.Tags);
        Assert.Contains("仙灵", token.Tags);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendActLilliaReprintRequiresUnreducedManaCost()
    {
        var state = LegendActiveAbilityState("UNL-230/219", "P1-LEGEND-LILLIA-REPRINT", mana: 4);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-lillia-reprint-legend-act", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-LILLIA-REPRINT",
                "LEGEND_DYNAMIC_PAY_EXHAUST_CREATE_FAERIE",
                [],
                ["SPEND_MANA:4"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Single(result.State.StackItems);
        Assert.DoesNotContain(result.Events, e => e.Kind == "UNIT_TOKEN_CREATED");
        var activationEvents = result.Events;
        result = await OfficialGraveyardRecastTests.Top(result.State);
        result = result with { Events = activationEvents.Concat(result.Events).ToArray() };
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Contains("P1-LEGEND-LILLIA-REPRINT-TOKEN-001", result.State.PlayerZones["P1"].Base);
        Assert.Equal("UNL·T07", result.State.CardObjects["P1-LEGEND-LILLIA-REPRINT-TOKEN-001"].CardNo);
    }

    [Fact]
    public async Task P79LegendStaticRumbleGrantsSteadfastToMechanicalDefender()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-MECHANICAL-DEFENDER"],
                    LegendZone = ["P2-LEGEND-RUMBLE"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-MECHANICAL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-MECHANICAL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "机械"],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-LEGEND-RUMBLE"] = new(
                    "P2-LEGEND-RUMBLE",
                    cardNo: "SFD·181/221",
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var rumbleLegendAura = Assert.Single(state.ContinuousEffects, effect =>
            string.Equals(effect.Layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(effect.SourceObjectId, "P2-LEGEND-RUMBLE", StringComparison.Ordinal));
        Assert.Equal("OBJECT", rumbleLegendAura.Scope);
        Assert.Equal("WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", rumbleLegendAura.Duration);
        Assert.Equal("P2-BATTLEFIELD-MECHANICAL-DEFENDER", rumbleLegendAura.TargetObjectId);
        Assert.Equal(0, rumbleLegendAura.PowerDelta);
        Assert.Equal(0, rumbleLegendAura.BasePower);
        Assert.Equal(0, rumbleLegendAura.EffectivePower);
        Assert.Empty(rumbleLegendAura.EffectKind);
        Assert.Null(rumbleLegendAura.SourceCardNo);
        Assert.Empty(rumbleLegendAura.SourcePath);
        Assert.Null(rumbleLegendAura.ParticipantObjectIds);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-rumble-static-battle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-ATTACKER"],
                ["P2-BATTLEFIELD-MECHANICAL-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        var defenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-MECHANICAL-DEFENDER", defenderDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-ATTACKER", defenderDamageEvent.Payload["targetObjectId"]);
        Assert.Equal("坚守", defenderDamageEvent.Payload["keyword"]);
        Assert.Equal(2, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(1, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(3, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, defenderDamageEvent.Payload["damage"]);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-ATTACKER"].Damage);
    }

    [Fact]
    public async Task P79LegendStaticLucianGrantsAssaultFromAttachedEquipment()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-ATTACKER",
                        "P1-BATTLEFIELD-ATTACHED-EQUIPMENT"
                    ],
                    LegendZone = ["P1-LEGEND-LUCIAN"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-ATTACHED-EQUIPMENT"] = new(
                    "P1-BATTLEFIELD-ATTACHED-EQUIPMENT",
                    tags: [CardObjectTags.EquipmentCard],
                    attachedToObjectId: "P1-BATTLEFIELD-ATTACKER",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-LUCIAN"] = new(
                    "P1-LEGEND-LUCIAN",
                    cardNo: "SFD·183/221",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-lucian-static-battle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-ATTACKER"],
                ["P2-BATTLEFIELD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        var attackerDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "ATTACKER", StringComparison.Ordinal));
        Assert.Equal("P1-BATTLEFIELD-ATTACKER", attackerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal("P2-BATTLEFIELD-DEFENDER", attackerDamageEvent.Payload["targetObjectId"]);
        Assert.Equal("强攻", attackerDamageEvent.Payload["keyword"]);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(1, attackerDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(3, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(3, attackerDamageEvent.Payload["damage"]);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-ATTACKER"].Damage);
    }

    [Fact]
    public async Task P79LegendStaticMasterYiGrantsSingleDefenderPowerBonus()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-DEFENDER"],
                    LegendZone = ["P2-LEGEND-MASTER-YI"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-LEGEND-MASTER-YI"] = new(
                    "P2-LEGEND-MASTER-YI",
                    cardNo: "OGS·019/024",
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-master-yi-static-battle", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-BATTLEFIELD-ATTACKER"],
                ["P2-BATTLEFIELD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        var defenderDamageEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["combatRole"] as string, "DEFENDER", StringComparison.Ordinal));
        Assert.Equal("P2-BATTLEFIELD-DEFENDER", defenderDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-BATTLEFIELD-ATTACKER", defenderDamageEvent.Payload["targetObjectId"]);
        Assert.Equal("坚守", defenderDamageEvent.Payload["keyword"]);
        Assert.Equal(2, defenderDamageEvent.Payload["basePower"]);
        Assert.Equal(0, defenderDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(2, defenderDamageEvent.Payload["staticPowerBonus"]);
        Assert.Equal(4, defenderDamageEvent.Payload["combatPower"]);
        Assert.Equal(4, defenderDamageEvent.Payload["damage"]);
        Assert.Equal(0, result.State.CardObjects["P1-BATTLEFIELD-ATTACKER"].Damage);
    }

    [Fact]
    public async Task P79LegendStaticAhriReducesEnemyAttackerPower()
    {
        // Retain the historical method identity; the printed skill now resolves
        // as a turn modifier before combat, rather than an implicit static bonus.
        var state = EnemyAttackMightTests.Board();
        foreach (var id in new[] { "ENEMY2", "ENEMY3" })
            state = LocalDestructionRecallTests.At(state, id, "P2", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { CardNo = "SFD·125/221" },
            ["ENEMY1"] = state.CardObjects["ENEMY1"] with { CardNo = "SFD·125/221", Power = 1 } } };
        var pending = await EnemyAttackMightTests.Pending(state);
        Assert.Equal(3, pending.CardObjects["HOST"].Power);
        var reduced = await AttackDamageDistributionTests.Top(pending);
        Assert.Equal(2, reduced.State.CardObjects["HOST"].Power);
        var closed = await AttackDamageDistributionTests.Top(reduced.State);
        var combat = await OfficialGraveyardRecastTests.Act(closed.State, "P1",
            new DeclareBattleCommand("F", ["HOST"], ["ENEMY1"], ["COMBAT_ASSIGNMENT"]));
        Assert.Empty(combat.State.StackItems); Assert.Empty(combat.State.TriggerQueue);
        var result = combat;
        var attackerDamageEvent = Assert.Single(result.Events, e => e.Kind == "DAMAGE_APPLIED"
            && Equals(e.Payload.GetValueOrDefault("combatRole"), "ATTACKER"));
        Assert.Equal("HOST", attackerDamageEvent.Payload["sourceObjectId"]);
        Assert.Equal("ENEMY1", attackerDamageEvent.Payload["targetObjectId"]);
        Assert.Equal("强攻", attackerDamageEvent.Payload["keyword"]);
        Assert.Equal(2, attackerDamageEvent.Payload["basePower"]);
        Assert.Equal(0, attackerDamageEvent.Payload["keywordBonus"]);
        Assert.Equal(0, attackerDamageEvent.Payload.GetValueOrDefault("staticPowerBonus", 0));
        Assert.Equal(2, attackerDamageEvent.Payload["combatPower"]);
        Assert.Equal(2, attackerDamageEvent.Payload["damage"]);
        Assert.Equal(0, result.State.CardObjects["HOST"].Damage);
        OfficialGraveyardRecastTests.Restore(result.State);
        ArmamentMightTests.PublicRecovery(result.State);
    }

    [Fact]
    public async Task P79LegendTriggerDravenDrawsWhenControllerWinsBattle()
    {
        // CN 468.3-468.5: the earned skill has its real response window before
        // control. Keep the original draw/survival assertions after both passes.
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD-ATTACKER"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-BATTLEFIELD-ATTACKER", "OGN·142/298", "P1"),
            ["P1-LEGEND-DRAVEN"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-LEGEND-DRAVEN", "SFD·185/221", "P1"),
            ["P1-DRAVEN-DRAW-001"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-DRAVEN-DRAW-001", "OGN·219/298", "P1"),
            ["P2-BATTLEFIELD-DEFENDER"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-BATTLEFIELD-DEFENDER", "OGN·219/298", "P2"),
            ["DRAVEN-BATTLEFIELD"] = OfficialGarenConquestBattlefieldCountTests.Printed("DRAVEN-BATTLEFIELD", "OGN·296/298", "P2") with { ControllerId = "P2" }
        };
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BATTLEFIELD-ATTACKER"], LegendZone = ["P1-LEGEND-DRAVEN"],
                    MainDeck = ["P1-DRAVEN-DRAW-001"]
                },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["DRAVEN-BATTLEFIELD", "P2-BATTLEFIELD-DEFENDER"] }
            },
            CardObjects = cards,
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ATTACKER"] = new("P1", "BASE"),
                ["P1-LEGEND-DRAVEN"] = new("P1", "LEGEND"),
                ["P1-DRAVEN-DRAW-001"] = new("P1", "MAIN_DECK"),
                ["P2-BATTLEFIELD-DEFENDER"] = new("P2", "BATTLEFIELD", "DRAVEN-BATTLEFIELD"),
                ["DRAVEN-BATTLEFIELD"] = new("P2", "BATTLEFIELD", "DRAVEN-BATTLEFIELD")
            }
        };
        var moved = await OfficialGraveyardRecastTests.Act(state, "P1",
            new MoveUnitCommand("P1-BATTLEFIELD-ATTACKER", "BASE", "BATTLEFIELD:DRAVEN-BATTLEFIELD", []));
        var firstPass = await OfficialGraveyardRecastTests.Act(moved.State, "P1", new PassFocusCommand());
        var secondPass = await OfficialGraveyardRecastTests.Act(firstPass.State, "P2", new PassFocusCommand());
        var result = await OfficialGraveyardRecastTests.Act(secondPass.State, "P1",
            new DeclareBattleCommand("DRAVEN-BATTLEFIELD", ["P1-BATTLEFIELD-ATTACKER"], ["P2-BATTLEFIELD-DEFENDER"], ["COMBAT_ASSIGNMENT"]));
        Assert.NotNull(result.State.PendingBattleResult);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal("P2", result.State.CardObjects["DRAVEN-BATTLEFIELD"].ControllerId);
        var events = result.Events.ToList();
        for (var i = 0; i < 2; i++)
        {
            Assert.NotNull(result.State.PriorityPlayerId);
            result = await OfficialGraveyardRecastTests.Act(result.State, result.State.PriorityPlayerId!, new PassPriorityCommand());
            events.AddRange(result.Events);
        }
        result = result with { Events = events };
        Assert.Null(result.State.PendingBattleResult);
        Assert.True(events.FindIndex(e => e.Kind == "CARD_DRAWN") < events.FindIndex(e => e.Kind == "BATTLEFIELD_CONQUERED"));
        Assert.Equal("P1", result.State.CardObjects["DRAVEN-BATTLEFIELD"].ControllerId);
        Assert.True(result.Accepted);
        Assert.Equal(["P1-DRAVEN-DRAW-001"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].MainDeck);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLE_WON_DRAW_ONE", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.DoesNotContain("P2-BATTLEFIELD-DEFENDER", result.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("P1-BATTLEFIELD-ATTACKER", result.State.PlayerZones["P1"].Battlefields);
        Assert.Null(result.State.WinnerPlayerId);
        OfficialGraveyardRecastTests.Restore(result.State);
        ArmamentMightTests.PublicRecovery(result.State);
    }

    [Fact]
    public async Task P79LegendTriggerGarenDrawsTwoOnConquerWithFourBattlefieldUnits()
    {
        var attackers = new[]
        {
            "P1-BATTLEFIELD-ATTACKER", "P1-BATTLEFIELD-SUPPORT-001",
            "P1-BATTLEFIELD-SUPPORT-002", "P1-BATTLEFIELD-SUPPORT-003"
        };
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD-ATTACKER"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-BATTLEFIELD-ATTACKER", "OGN·142/298", "P1"),
            ["P1-LEGEND-GAREN"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-LEGEND-GAREN", "OGS·023/024", "P1"),
            ["P1-GAREN-DRAW-001"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-GAREN-DRAW-001", "OGN·142/298", "P1"),
            ["P1-GAREN-DRAW-002"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-GAREN-DRAW-002", "OGN·142/298", "P1"),
            ["P2-BATTLEFIELD-DEFENDER"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-BATTLEFIELD-DEFENDER", "OGN·219/298", "P2"),
            ["GAREN-BATTLEFIELD"] = OfficialGarenConquestBattlefieldCountTests.Printed("GAREN-BATTLEFIELD", "OGN·296/298", "P2") with { ControllerId = "P2" }
        };
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["P1-LEGEND-GAREN"] = new("P1", "LEGEND"),
            ["P1-GAREN-DRAW-001"] = new("P1", "MAIN_DECK"),
            ["P1-GAREN-DRAW-002"] = new("P1", "MAIN_DECK"),
            ["P2-BATTLEFIELD-DEFENDER"] = new("P2", "BATTLEFIELD", "GAREN-BATTLEFIELD"),
            ["GAREN-BATTLEFIELD"] = new("P2", "BATTLEFIELD", "GAREN-BATTLEFIELD")
        };
        foreach (var id in attackers)
        {
            if (id != "P1-BATTLEFIELD-ATTACKER")
                cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·219/298", "P1");
            locations[id] = new("P1", "BASE");
        }
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = attackers, LegendZone = ["P1-LEGEND-GAREN"],
                    MainDeck = ["P1-GAREN-DRAW-001", "P1-GAREN-DRAW-002"]
                },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["GAREN-BATTLEFIELD", "P2-BATTLEFIELD-DEFENDER"] }
            },
            CardObjects = cards,
            ObjectLocations = locations
        };
        var moved = await OfficialGraveyardRecastTests.Act(state, "P1",
            new MoveUnitCommand(attackers[0], "BASE", "BATTLEFIELD:GAREN-BATTLEFIELD", [], attackers));
        var firstPass = await OfficialGraveyardRecastTests.Act(moved.State, "P1", new PassFocusCommand());
        var secondPass = await OfficialGraveyardRecastTests.Act(firstPass.State, "P2", new PassFocusCommand());
        var result = await new CoreRuleEngine().ResolveAsync(
            secondPass.State,
            new PlayerIntent("intent-p7-9-garen-trigger-conquer", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "GAREN-BATTLEFIELD",
                attackers,
                ["P2-BATTLEFIELD-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);
        result = await CombatTestDriver.FinishAsync(result);

        Assert.True(result.Accepted);
        Assert.Equal(["P1-GAREN-DRAW-001", "P1-GAREN-DRAW-002"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].MainDeck);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_CONQUERED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_DRAW_TWO", StringComparison.Ordinal));
        var drawEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Equal(2, drawEvent.Payload["count"]);
        Assert.Null(result.State.WinnerPlayerId);
    }

    [Fact]
    public async Task P79LegendTriggerViReadiesUnitOnOverkillConquer()
    {
        var opened = await OfficialLegendConquestTests.CombatConquer(OfficialLegendConquestTests.CombatPosition(power: 5));
        Assert.Equal(4, Assert.Single(opened.Events, e => e.Kind == "BATTLEFIELD_CONQUERED").Payload["assignedOverkillDamageToEnemyUnits"]);
        Assert.False(opened.State.CardObjects["LEGEND"].IsExhausted);
        var paid = await OfficialLegendConquestTests.Choose(opened.State, "TARGET");
        Assert.True(paid.State.CardObjects["LEGEND"].IsExhausted); Assert.True(paid.State.CardObjects["TARGET"].IsExhausted);
        var done = await OfficialGraveyardRecastTests.Top(paid.State);
        Assert.False(done.State.CardObjects["TARGET"].IsExhausted);
        Assert.Contains(done.Events, e => e.Kind == "UNIT_READIED" && Equals(e.Payload.GetValueOrDefault("targetObjectId"), "TARGET"));
    }

    [Fact]
    public async Task P79LegendTriggerViRequiresThreeOverkillOnConquer()
    {
        var done = await OfficialLegendConquestTests.CombatConquer(OfficialLegendConquestTests.CombatPosition("UNL-229/219", power: 3));
        Assert.Equal(2, Assert.Single(done.Events, e => e.Kind == "BATTLEFIELD_CONQUERED").Payload["assignedOverkillDamageToEnemyUnits"]);
        Assert.Null(done.State.PendingCardChoice); Assert.False(done.State.CardObjects["LEGEND"].IsExhausted);
        Assert.True(done.State.CardObjects["TARGET"].IsExhausted);
    }

    [Fact]
    public async Task P79LegendTriggerVexDrawsWhenControllerHoldsBattlefieldDoesNotTriggerOnDefensiveVictory()
    {
        var state = VexBattlefieldHoldState("UNL-193/219", "P2-LEGEND-VEX", legendExhausted: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-vex-battlefield-held", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-VEX-ATTACKER"],
                ["P2-VEX-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79LegendTriggerVexRequiresActiveLegend()
    {
        var state = VexBattlefieldHoldState("UNL-232/219", "P2-LEGEND-VEX-REPRINT", legendExhausted: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-vex-battlefield-held-rejected", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-VEX-ATTACKER"],
                ["P2-VEX-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(result.State.CardObjects["P2-LEGEND-VEX-REPRINT"].IsExhausted);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_DRAW_ONE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendActIreliaReadiesPendingTargetedFriendlyUnit()
    {
        var state = IreliaPendingFriendlyTargetState("SFD·195/221", "P1-LEGEND-IRELIA", targetFriendlyUnit: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-irelia-targeted-friendly-ready", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-IRELIA",
                "LEGEND_REACTION_PAY_1_EXHAUST_READY_TARGETED_FRIENDLY_UNIT",
                ["P1-LEGEND-BATTLEFIELD-UNIT"],
                ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(result.State.CardObjects["P1-LEGEND-IRELIA"].IsExhausted);
        Assert.False(result.State.CardObjects["P1-LEGEND-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_ABILITY_ACTIVATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, "LEGEND_REACTION_PAY_1_EXHAUST_READY_TARGETED_FRIENDLY_UNIT", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-LEGEND-BATTLEFIELD-UNIT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendActIreliaRequiresPendingFriendlyUnitTarget()
    {
        var state = IreliaPendingFriendlyTargetState("SFD·246/221", "P1-LEGEND-IRELIA-REPRINT", targetFriendlyUnit: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-irelia-targeted-friendly-rejected", "P1", "LEGEND_ACT"),
            new LegendActCommand(
                "P1-LEGEND-IRELIA-REPRINT",
                "LEGEND_REACTION_PAY_1_EXHAUST_READY_TARGETED_FRIENDLY_UNIT",
                ["P1-LEGEND-BATTLEFIELD-UNIT"],
                ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(result.State.CardObjects["P1-LEGEND-IRELIA-REPRINT"].IsExhausted);
        Assert.True(result.State.CardObjects["P1-LEGEND-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
    }

    [Fact]
    public async Task P79LegendTriggerIreliaPaysOneToReadyLegendOnConquer()
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(OfficialLegendConquestTests.Position("SFD·195a/221·P", mana: 1));
        Assert.True(opened.State.CardObjects["LEGEND"].IsExhausted); Assert.Equal(1, opened.State.RunePools["P1"].Mana);
        var paid = await OfficialLegendConquestTests.Pay(opened.State, true);
        Assert.True(paid.State.CardObjects["LEGEND"].IsExhausted); Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        var done = await OfficialGraveyardRecastTests.Top(paid.State); Assert.False(done.State.CardObjects["LEGEND"].IsExhausted);
    }

    [Fact]
    public async Task P79LegendTriggerIreliaRequiresManaToReadyLegendOnConquer()
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(OfficialLegendConquestTests.Position("SFD·195/221", mana: 0));
        var payment = opened.State.PendingPayment!;
        var rejected = await new CoreRuleEngine().ResolveAsync(opened.State, new("poor", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(opened.State), MatchStateHasher.Hash(rejected.State));
        Assert.True(rejected.State.CardObjects["LEGEND"].IsExhausted);
    }

    [Fact]
    public async Task P79LegendTriggerRenataCreatesDormantGoldWhenControllerHoldsBattlefieldDoesNotTriggerOnDefensiveVictory()
    {
        var state = RenataBattlefieldHoldState("SFD·201/221", "P2-LEGEND-RENATA", legendExhausted: false, score: 5);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-renata-battlefield-held", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-RENATA-ATTACKER"],
                ["P2-RENATA-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79LegendTriggerRenataGoldBonusRequiresScoreWithinThreeDoesNotTriggerOnDefensiveVictory()
    {
        var state = RenataBattlefieldHoldState("SFD·249/221", "P2-LEGEND-RENATA-REPRINT", legendExhausted: false, score: 4);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-renata-battlefield-held-no-bonus", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-RENATA-ATTACKER"],
                ["P2-RENATA-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79LegendTriggerRenataRequiresActiveLegend()
    {
        var state = RenataBattlefieldHoldState("SFD·201/221", "P2-LEGEND-RENATA", legendExhausted: true, score: 5);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-renata-battlefield-held-rejected", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-RENATA-ATTACKER"],
                ["P2-RENATA-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(result.State.CardObjects["P2-LEGEND-RENATA"].IsExhausted);
        Assert.Empty(result.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_CREATE_GOLD", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendTriggerLeblancCreatesActiveImageOnConquer()
    {
        var triggered = await ConquestLifecycleRegressionTests.Conquer(OfficialLeblancCreationTests.Position());
        Assert.False(triggered.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Empty(OfficialTokenReplacementTests.Tokens(triggered.State));
        Assert.Contains(triggered.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
        var pending = triggered;
        var result = await OfficialTokenReplacementTests.Choose(pending.State, "H2");
        var costEvents = result.Events;
        result = await OfficialGraveyardRecastTests.Top(result.State);
        result = result with { Events = costEvents.Concat(result.Events).ToArray() };
        Assert.True(result.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(["H2"], result.State.PlayerZones["P1"].Graveyard);
        var image = Assert.Single(OfficialTokenReplacementTests.Tokens(result.State));
        Assert.Equal(0, image.Power); Assert.False(image.IsExhausted);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, image.TokenFactoryCardNo);
        var entry = Assert.Single(result.Events, e => e.Kind == "UNIT_TOKEN_CREATED");
        Assert.Equal("BATTLEFIELD", entry.Payload["destinationZone"]); Assert.Equal("BF", entry.Payload["battlefieldId"]);
        Assert.False(entry.Payload.ContainsKey("copiedTargetObjectId")); // Target is selected only after entry.
        var confirmed = await OfficialTokenReplacementTests.Choose(result.State, "UNIT");
        var copied = await OfficialGraveyardRecastTests.Top(confirmed.State);
        var token = copied.State.CardObjects[image.ObjectId];
        Assert.Equal(1, token.Power); Assert.Equal("OGN·096/298", token.CardNo);
        Assert.Contains(CardObjectTags.UnitCard, token.Tags); Assert.Contains(CardObjectTags.Ephemeral, token.Tags);
    }

    [Fact]
    public async Task P79LegendTriggerLeblancCreatesActiveImageOnHoldDoesNotTriggerOnDefensiveVictory()
    {
        var state = LeblancBattlefieldHoldState("UNL-235/219", "P2-LEGEND-LEBLANC-REPRINT", hasDiscard: true, legendExhausted: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-leblanc-battlefield-held", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-LEBLANC-ATTACKER"],
                ["P2-LEBLANC-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79LegendTriggerLeblancRequiresDiscardAndActiveLegend()
    {
        var noDiscardState = LeblancBattlefieldHoldState("UNL-235*/219", "P2-LEGEND-LEBLANC-ALT", hasDiscard: false, legendExhausted: false);

        var noDiscardResult = await new CoreRuleEngine().ResolveAsync(
            noDiscardState,
            new PlayerIntent("intent-p7-9-leblanc-battlefield-held-no-discard", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-LEBLANC-ATTACKER"],
                ["P2-LEBLANC-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(noDiscardResult.Accepted);
        Assert.False(noDiscardResult.State.CardObjects["P2-LEGEND-LEBLANC-ALT"].IsExhausted);
        Assert.Empty(noDiscardResult.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(noDiscardResult.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_CREATE_IMAGE", StringComparison.Ordinal));

        var exhaustedLegendState = LeblancBattlefieldHoldState("UNL-199/219", "P2-LEGEND-LEBLANC", hasDiscard: true, legendExhausted: true);

        var exhaustedLegendResult = await new CoreRuleEngine().ResolveAsync(
            exhaustedLegendState,
            new PlayerIntent("intent-p7-9-leblanc-battlefield-held-exhausted", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-LEBLANC-ATTACKER"],
                ["P2-LEBLANC-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(exhaustedLegendResult.Accepted);
        Assert.True(exhaustedLegendResult.State.CardObjects["P2-LEGEND-LEBLANC"].IsExhausted);
        Assert.Equal(["P2-LEBLANC-DISCARD"], exhaustedLegendResult.State.PlayerZones["P2"].Hand);
        Assert.Empty(exhaustedLegendResult.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(exhaustedLegendResult.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_CREATE_IMAGE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendTriggerLeblancSkipsOpponentControlledDiscardInHand()
    {
        var state = OfficialLeblancCreationTests.Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["H1"] = state.CardObjects["H1"] with { OwnerId = "P2", ControllerId = "P2" } } };
        var pending = await OfficialLeblancCreationTests.OpenCost(state);
        Assert.DoesNotContain("H1", pending.State.PendingCardChoice!.LegalObjectIds);
        var result = await OfficialTokenReplacementTests.Choose(pending.State, "H2");
        Assert.Equal(["H1", "H3"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["H2"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(result.Events, e => e.Kind == "CARD_DISCARDED" && Equals(e.Payload["targetObjectId"], "H2"));
        Assert.DoesNotContain(result.Events, e => e.Kind == "CARD_DISCARDED" && Equals(e.Payload["targetObjectId"], "H1"));
    }

    [Fact]
    public async Task P79LegendTriggerLeblancRequiresControlledLegendSource()
    {
        var baseState = LeblancBattlefieldConquerState("UNL-199/219", "P1-LEGEND-LEBLANC", hasDiscard: true);
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-LEBLANC"] = cardObjects["P1-LEGEND-LEBLANC"] with
        {
            OwnerId = "P2",
            ControllerId = "P2"
        };
        var state = baseState with
        {
            CardObjects = cardObjects
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-leblanc-dirty-legend", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-LEBLANC-ATTACKER"],
                ["P2-LEBLANC-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.False(result.State.CardObjects["P1-LEGEND-LEBLANC"].IsExhausted);
        Assert.Equal(["P1-LEBLANC-DISCARD"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_CONQUERED_CREATE_IMAGE", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["tokenName"] as string, "映像", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendTriggerReksaiRevealsTopTwoPlaysUnitAndRecyclesRestOnConquer()
    {
        var opened = await OfficialLegendDeckPlayTests.Open(OfficialLegendDeckPlayTests.Position());
        opened = await OfficialLegendDeckPlayTests.Select(opened.State, "DECK0");
        var done = await OfficialGraveyardRecastTests.Act(opened.State, "P1", new PlayCardCommand("DECK0", "SFD·125/221", []));
        Assert.True(done.State.CardObjects["DECK0"].IsExhausted); Assert.Equal(16, done.State.RunePools["P1"].Mana);
        Assert.Equal(["TAIL", "DECK1"], done.State.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task P79LegendTriggerReksaiMayDeclineAndRecycleBothNonUnitCards()
    {
        var opened = await OfficialLegendDeckPlayTests.Open(OfficialLegendDeckPlayTests.Position("SFD·243/221", top: ["OGN·048/298", "SFD·022/221"]));
        Assert.Equal(2, opened.State.PendingCardChoice!.LegalObjectIds.Count);
        var choice = opened.State.PendingCardChoice;
        var done = await OfficialGraveyardRecastTests.Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Equal("TAIL", done.State.PlayerZones["P1"].MainDeck[0]); Assert.Equal(3, done.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(2, Assert.Single(done.Events, e => e.Kind == "CARDS_RECYCLED").Payload["count"]);
    }

    [Fact]
    public async Task P79LegendTriggerReksaiExhaustedSourceCannotConfirm()
    {
        var done = await ConquestLifecycleRegressionTests.Conquer(OfficialLegendDeckPlayTests.Position(exhausted: true));
        Assert.Contains(done.Events, e => e.Kind == "TRIGGER_QUEUED"); Assert.Empty(done.State.StackItems);
        Assert.Null(done.State.PendingEffectPlay); Assert.Equal(["DECK0", "DECK1", "TAIL"], done.State.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task P79LegendTriggerIvernCreatesBrushBattlefieldTokenOnConquer()
    {
        var opened = await OfficialBattlefieldReplacementTests.Conquer(OfficialBattlefieldReplacementTests.Position());
        Assert.False(opened.State.CardObjects["LEGEND"].IsExhausted);
        var paid = await OfficialBattlefieldReplacementTests.Choose(opened.State, "LEGEND");
        Assert.True(paid.State.CardObjects["LEGEND"].IsExhausted);
        var done = await OfficialGraveyardRecastTests.Top(paid.State);
        OfficialBattlefieldReplacementTests.AssertBrush(paid.State, done.State);
    }

    [Fact]
    public async Task P79LegendTriggerIvernCreatesBrushBattlefieldTokenOnHoldDoesNotTriggerOnDefensiveVictory()
    {
        var state = IvernBattlefieldResultState("UNL-233/219", "P2-LEGEND-IVERN-REPRINT", controllerHolds: true, legendExhausted: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-ivern-battlefield-held", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-IVERN-ATTACKER"],
                ["P2-IVERN-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_HELD");
        Assert.DoesNotContain(result.State.TriggerQueue, t => t.HeldContext is not null);
        Assert.DoesNotContain(result.State.StackItems, t => t.HeldContext is not null);
        Assert.Empty(result.State.DelayedResourceGains);
    }

    [Fact]
    public async Task P79LegendTriggerIvernRequiresActiveLegend()
    {
        var state = IvernBattlefieldResultState("UNL-233*/219", "P2-LEGEND-IVERN-ALT", controllerHolds: true, legendExhausted: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-ivern-battlefield-held-exhausted", "P1", "DECLARE_BATTLE"),
            new DeclareBattleCommand(
                "BATTLEFIELD:P1-MAIN",
                ["P1-IVERN-ATTACKER"],
                ["P2-IVERN-DEFENDER"],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(result.State.CardObjects["P2-LEGEND-IVERN-ALT"].IsExhausted);
        Assert.DoesNotContain(result.State.PlayerZones["P2"].Battlefields, objectId =>
            objectId.StartsWith("P2-LEGEND-IVERN-ALT-TOKEN-", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "LEGEND_TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, "BATTLEFIELD_HELD_REPLACE_WITH_BRUSH", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P79LegendTriggerSettConsumesBoonAndRecallsDestroyedUnit()
    {
        var opened = await OfficialSettReplacementTests.Open();
        Assert.NotNull(opened.State.PendingRuleChoice);
        var done = await OfficialSettReplacementTests.Choose(opened.State);
        Assert.Contains("D", done.State.PlayerZones["P2"].Base);
        Assert.DoesNotContain(CardObjectTags.Boon, done.State.CardObjects["D"].Tags);
        Assert.Equal(7, done.State.RunePools["P2"].Mana); Assert.Equal(0, done.State.RunePools["P2"].Power);
    }

    [Fact]
    public async Task P79LegendTriggerSettReplacementDebitsPowerAfterXerathSkillCleanup()
    {
        var state = OfficialSettReplacementTests.Position();
        state = state with { TimingState = TimingStates.NeutralClosed, PriorityPlayerId = "P1", PassedPriorityPlayerIds = ["P2"],
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["A"] = state.CardObjects["A"] with { CardNo = P4ActivatedAbilityCatalog.XerathCardNo } },
            StackItems = [new StackItemState("xerath", "P1", "A", P4ActivatedAbilityCatalog.XerathDamageAbilityEffectKind,
                P4ActivatedAbilityCatalog.XerathCardNo, ["D"], damageAmount: P4ActivatedAbilityCatalog.XerathDamageAbilityDamageAmount)] };
        var opened = await OfficialGraveyardRecastTests.Act(state, "P1", new PassPriorityCommand());
        var done = await OfficialSettReplacementTests.Choose(opened.State);
        Assert.Contains("D", done.State.PlayerZones["P2"].Base);
        Assert.Equal(7, done.State.RunePools["P2"].Mana); Assert.Equal(0, done.State.RunePools["P2"].Power);
        Assert.Contains(done.Events, e => e.Kind == "DAMAGE_APPLIED");
        Assert.DoesNotContain(done.Events, e => e.Kind == "UNIT_DESTROYED" && e.Payload.GetValueOrDefault("targetObjectId") as string == "D");
    }

    [Fact]
    public async Task P79LegendTriggerSettReadiesOnConquer()
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(OfficialLegendConquestTests.Position("OGN·310/298"));
        Assert.True(opened.State.CardObjects["LEGEND"].IsExhausted); Assert.Single(opened.State.StackItems);
        var done = await OfficialGraveyardRecastTests.Top(opened.State);
        Assert.False(done.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Contains(done.Events, e => e.Kind == "LEGEND_READIED");
    }

    [Fact]
    public async Task P79LegendTriggerSettReplacementRequiresPowerAndActiveLegend()
    {
        var done = await OfficialSettReplacementTests.Open(OfficialSettReplacementTests.Position(power: 0));
        Assert.Null(done.State.PendingRuleChoice); Assert.Contains("D", done.State.PlayerZones["P2"].Graveyard);
        Assert.False(done.State.CardObjects["LEGEND"].IsExhausted); Assert.Equal(7, done.State.RunePools["P2"].Mana);
    }
}
