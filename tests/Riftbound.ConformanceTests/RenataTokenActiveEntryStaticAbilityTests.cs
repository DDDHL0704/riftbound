using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RenataTokenActiveEntryStaticAbilityTests
{
    private const string RenataCardNo = "SFD·171/221";
    private const string RenataObjectId = "P1-RENATA";
    private const string AzirLegendCardNo = "SFD·197/221";
    private const string AzirLegendObjectId = "P1-LEGEND-AZIR";
    private const string AzirSandSoldierAbilityId = "LEGEND_PAY_1_EXHAUST_CREATE_SAND_SOLDIER_AFTER_ARMAMENT";
    private const string PykeLegendCardNo = "UNL-185/219";
    private const string PykeLegendObjectId = "P1-LEGEND-PYKE";
    private const string PykeGoldAbilityId = "LEGEND_PAY_1_EXHAUST_RECALL_BATTLEFIELD_UNIT_CREATE_COIN";
    private const string PykeTargetUnitObjectId = "P1-PYKE-TARGET-UNIT";

    [Fact]
    public async Task BehaviorSpecCatalogParsesRenataUnitTokenActiveEntryStaticAbility()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        foreach (var cardNo in new[] { RenataCardNo, "SFD·171a/221" })
        {
            var renata = Assert.Single(specs, spec => string.Equals(spec.CardNo, cardNo, StringComparison.Ordinal));
            var ability = Assert.Single(
                renata.StaticAbilities,
                candidate => string.Equals(candidate.Kind, StaticAbilityKinds.FriendlyFilteredUnitsEnterReady, StringComparison.Ordinal));

            Assert.Equal(StaticAbilityKinds.FriendlyFilteredUnitsEnterReady, ability.Kind);
            Assert.Equal(StaticAuraTargetFilters.Token, ability.TargetFilter);
            Assert.Contains("你的指示物以活跃状态进场", ability.Text, StringComparison.Ordinal);
            Assert.Equal(BehaviorImplementationStatuses.Implemented, ability.Status);
        }
    }

    [Fact]
    public async Task RenataMakesFriendlyUnitTokenEnterReadyFromStaticAbilitySpec()
    {
        var result = await ActivateAzirSandSoldierAsync(BuildAzirWithRenataState());

        Assert.True(result.Accepted, result.ErrorMessage);
        var tokenObjectId = "P1-LEGEND-AZIR-TOKEN-001";
        Assert.Contains(tokenObjectId, result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects[tokenObjectId].IsExhausted);

        var tokenEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Equal(tokenObjectId, tokenEvent.Payload["tokenObjectId"]);
        Assert.Equal("SFD·T02", tokenEvent.Payload["tokenCardNo"]);
        Assert.Equal(false, tokenEvent.Payload["isExhausted"]);
        Assert.Equal(StaticAbilityKinds.FriendlyFilteredUnitsEnterReady, tokenEvent.Payload["entryStaticAbilityKind"]);
        Assert.Equal(RenataObjectId, tokenEvent.Payload["entryStaticAbilitySourceObjectId"]);
        Assert.Equal(RenataCardNo, tokenEvent.Payload["entryStaticAbilitySourceCardNo"]);
    }

    [Fact]
    public async Task FaceDownRenataDoesNotApplyUnitTokenActiveEntryStaticAbility()
    {
        var result = await ActivateAzirSandSoldierAsync(BuildAzirWithRenataState(faceDownRenata: true));

        Assert.True(result.Accepted, result.ErrorMessage);
        var tokenEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Equal("SFD·T02", tokenEvent.Payload["tokenCardNo"]);
        Assert.False(tokenEvent.Payload.ContainsKey("entryStaticAbilityKind"));
        Assert.False(tokenEvent.Payload.ContainsKey("entryStaticAbilitySourceObjectId"));
    }

    [Fact]
    public async Task RenataMakesFriendlyEquipmentTokenEnterReadyFromStaticAbilitySpec()
    {
        var result = await ActivatePykeGoldAsync(BuildPykeWithRenataState());

        Assert.True(result.Accepted, result.ErrorMessage);
        var tokenObjectId = "P1-LEGEND-PYKE-TOKEN-001";
        Assert.Contains(tokenObjectId, result.State.PlayerZones["P1"].Base);
        var token = result.State.CardObjects[tokenObjectId];
        Assert.False(token.IsExhausted);
        Assert.Equal("UNL·T05", token.CardNo);
        Assert.Contains(CardObjectTags.EquipmentCard, token.Tags);
        Assert.Contains("金币", token.Tags);

        var tokenEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Equal(tokenObjectId, tokenEvent.Payload["tokenObjectId"]);
        Assert.Equal("UNL·T05", tokenEvent.Payload["tokenCardNo"]);
        Assert.Equal("金币", tokenEvent.Payload["tokenName"]);
        Assert.Equal(false, tokenEvent.Payload["isExhausted"]);
        Assert.Equal(StaticAbilityKinds.FriendlyFilteredUnitsEnterReady, tokenEvent.Payload["entryStaticAbilityKind"]);
        Assert.Equal(RenataObjectId, tokenEvent.Payload["entryStaticAbilitySourceObjectId"]);
        Assert.Equal(RenataCardNo, tokenEvent.Payload["entryStaticAbilitySourceCardNo"]);
    }

    [Fact]
    public async Task FaceDownRenataDoesNotApplyEquipmentTokenActiveEntryStaticAbility()
    {
        var result = await ActivatePykeGoldAsync(BuildPykeWithRenataState(faceDownRenata: true));

        Assert.True(result.Accepted, result.ErrorMessage);
        var tokenObjectId = "P1-LEGEND-PYKE-TOKEN-001";
        Assert.Contains(tokenObjectId, result.State.PlayerZones["P1"].Base);
        Assert.True(result.State.CardObjects[tokenObjectId].IsExhausted);

        var tokenEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Equal(true, tokenEvent.Payload["isExhausted"]);
        Assert.Equal("UNL·T05", tokenEvent.Payload["tokenCardNo"]);
        Assert.False(tokenEvent.Payload.ContainsKey("entryStaticAbilityKind"));
        Assert.False(tokenEvent.Payload.ContainsKey("entryStaticAbilitySourceObjectId"));
    }

    private static async Task<ResolutionResult> ActivateAzirSandSoldierAsync(MatchState state)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-renata-token-entry-azir", "P1", CommandTypes.LegendAct),
            new LegendActCommand(
                AzirLegendObjectId,
                AzirSandSoldierAbilityId,
                [],
                ["SPEND_MANA:1"]),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ActivatePykeGoldAsync(MatchState state)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-renata-token-entry-pyke-gold", "P1", CommandTypes.LegendAct),
            new LegendActCommand(
                PykeLegendObjectId,
                PykeGoldAbilityId,
                [PykeTargetUnitObjectId],
                ["SPEND_MANA:1"]),
            CancellationToken.None);
    }

    private static MatchState BuildAzirWithRenataState(bool faceDownRenata = false)
    {
        var renataTags = new[] { CardObjectTags.UnitCard }
            .Concat(faceDownRenata ? [CardObjectTags.Standby] : Array.Empty<string>())
            .ToArray();

        return new MatchState(
            "renata-token-active-entry",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "s1",
                ["P2"] = "s2"
            }) with
        {
            TurnPlayerId = "P1",
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [RenataObjectId],
                    LegendZone = [AzirLegendObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [RenataObjectId] = new(
                    RenataObjectId,
                    isFaceDown: faceDownRenata,
                    power: 4,
                    tags: renataTags,
                    cardNo: RenataCardNo,
                    ownerId: "P1",
                    controllerId: "P1"),
                [AzirLegendObjectId] = new(
                    AzirLegendObjectId,
                    cardNo: AzirLegendCardNo,
                    ownerId: "P1",
                    controllerId: "P1")
            },
            UntilEndOfTurnEffects = ["PLAYED_ARMAMENT_THIS_TURN:P1"]
        };
    }

    private static MatchState BuildPykeWithRenataState(bool faceDownRenata = false)
    {
        var renataTags = new[] { CardObjectTags.UnitCard }
            .Concat(faceDownRenata ? [CardObjectTags.Standby] : Array.Empty<string>())
            .ToArray();

        return new MatchState(
            "renata-equipment-token-active-entry",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "s1",
                ["P2"] = "s2"
            }) with
        {
            TurnPlayerId = "P1",
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [RenataObjectId],
                    Battlefields = [PykeTargetUnitObjectId],
                    LegendZone = [PykeLegendObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [RenataObjectId] = new(
                    RenataObjectId,
                    isFaceDown: faceDownRenata,
                    power: 4,
                    tags: renataTags,
                    cardNo: RenataCardNo,
                    ownerId: "P1",
                    controllerId: "P1"),
                [PykeLegendObjectId] = new(
                    PykeLegendObjectId,
                    cardNo: PykeLegendCardNo,
                    ownerId: "P1",
                    controllerId: "P1"),
                [PykeTargetUnitObjectId] = new(
                    PykeTargetUnitObjectId,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "SFD·125/221",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static IReadOnlyList<ImplementedCardBehavior> ImplementedBehaviors(
        IReadOnlyList<OfficialCard> cards)
    {
        var playCardBehaviors = CardBehaviorRegistry.GetAll()
            .Select(behavior => new ImplementedCardBehavior(
                behavior.CardNo,
                behavior.EffectKind,
                behavior.DisplayName))
            .ToArray();

        return OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(cards, playCardBehaviors);
    }
}
