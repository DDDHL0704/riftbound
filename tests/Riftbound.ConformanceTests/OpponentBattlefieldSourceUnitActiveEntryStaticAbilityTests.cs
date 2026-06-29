using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OpponentBattlefieldSourceUnitActiveEntryStaticAbilityTests
{
    private const string OgnVayneCardNo = "OGN·035/298";
    private const string VayneObjectId = "P1-VAYNE";
    private const string OpponentBattlefieldObjectId = "P2-CONTROLLED-BATTLEFIELD";
    private const string OpponentBattlefieldUnitObjectId = "P2-BATTLEFIELD-UNIT";

    [Theory]
    [InlineData("OGN·035/298")]
    [InlineData("SFD·223/221")]
    [InlineData("SFD·223*/221")]
    public async Task CatalogParsesVayneOpponentControlledBattlefieldSourceUnitEnterReadyStaticAbility(
        string cardNo)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var spec = Assert.Single(specs, candidate => string.Equals(candidate.CardNo, cardNo, StringComparison.Ordinal));
        var ability = Assert.Single(
            spec.StaticAbilities,
            candidate => string.Equals(candidate.Kind, StaticAbilityKinds.SourceUnitEnterReady, StringComparison.Ordinal));

        Assert.Equal("如果对手已控制任意战场，则我以活跃状态进场", ability.Text);
        Assert.Equal(1, ability.RequiredOpponentControlledBattlefieldCount);
        Assert.Null(ability.RequiredPlayerExperience);
        Assert.Null(ability.MaxControllerHandCount);
        Assert.Null(ability.RequiredOtherControlledUnitTag);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, ability.Status);
    }

    [Fact]
    public async Task VayneEntersReadyWhenOpponentControlsPublicBattlefieldCard()
    {
        var engine = new CoreRuleEngine();
        var state = BuildVayneState(OpponentBattlefieldCase.PublicBattlefieldCard);

        var played = await PlayVayneAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State, "vayne-opponent-battlefield-ready");

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(VayneObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.False(resolved.State.CardObjects[VayneObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsVayneUnitPlayedEvent);
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(StaticAbilityKinds.SourceUnitEnterReady, unitEvent.Payload["entryStaticAbilityKind"]);
        Assert.Equal(VayneObjectId, unitEvent.Payload["entryStaticAbilitySourceObjectId"]);
        Assert.Equal(OgnVayneCardNo, unitEvent.Payload["entryStaticAbilitySourceCardNo"]);
    }

    [Theory]
    [InlineData(OpponentBattlefieldCase.None)]
    [InlineData(OpponentBattlefieldCase.OpponentUnitAtBattlefield)]
    public async Task VayneDoesNotEnterReadyWithoutOpponentControlledPublicBattlefieldCard(
        OpponentBattlefieldCase opponentBattlefieldCase)
    {
        var engine = new CoreRuleEngine();
        var state = BuildVayneState(opponentBattlefieldCase);

        var played = await PlayVayneAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State, $"vayne-no-opponent-battlefield-{opponentBattlefieldCase}");

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(VayneObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.True(resolved.State.CardObjects[VayneObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsVayneUnitPlayedEvent);
        Assert.Equal(true, unitEvent.Payload["isExhausted"]);
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilityKind"));
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilitySourceObjectId"));
    }

    private static async Task<ResolutionResult> PlayVayneAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-vayne-opponent-battlefield-static-entry-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                VayneObjectId,
                OgnVayneCardNo,
                []),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ResolveTopOfStackAsync(
        CoreRuleEngine engine,
        MatchState state,
        string intentPrefix)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-{intentPrefix}-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        return await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent($"intent-{intentPrefix}-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static bool IsVayneUnitPlayedEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, VayneObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, VayneObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "薇恩", StringComparison.Ordinal);
    }

    private static MatchState BuildVayneState(OpponentBattlefieldCase opponentBattlefieldCase)
    {
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                Hand = [VayneObjectId]
            },
            ["P2"] = PlayerZones.Empty
        };
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [VayneObjectId] = new(
                VayneObjectId,
                isExhausted: true,
                cardNo: OgnVayneCardNo,
                ownerId: "P1",
                controllerId: "P1")
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [VayneObjectId] = new("P1", "HAND")
        };

        if (opponentBattlefieldCase is OpponentBattlefieldCase.PublicBattlefieldCard)
        {
            playerZones["P2"] = playerZones["P2"] with
            {
                Battlefields = [OpponentBattlefieldObjectId]
            };
            cardObjects[OpponentBattlefieldObjectId] = BuildOpponentBattlefieldCardObject();
            objectLocations[OpponentBattlefieldObjectId] = new("P2", "BATTLEFIELD");
        }
        else if (opponentBattlefieldCase is OpponentBattlefieldCase.OpponentUnitAtBattlefield)
        {
            playerZones["P2"] = playerZones["P2"] with
            {
                Battlefields = [OpponentBattlefieldUnitObjectId]
            };
            cardObjects[OpponentBattlefieldUnitObjectId] = new(
                OpponentBattlefieldUnitObjectId,
                isExhausted: true,
                tags: [CardObjectTags.UnitCard],
                cardNo: "SFD·006/221",
                ownerId: "P2",
                controllerId: "P2");
            objectLocations[OpponentBattlefieldUnitObjectId] = new("P2", "BATTLEFIELD", OpponentBattlefieldObjectId);
        }

        return new MatchState(
            "vayne-opponent-battlefield-active-entry-static-ability",
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
                ["P1"] = new(4, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };
    }

    private static CardObjectState BuildOpponentBattlefieldCardObject()
    {
        return new CardObjectState(
            OpponentBattlefieldObjectId,
            isExhausted: false,
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            cardNo: P6TokenFactoryCatalog.BrushBattlefieldTokenCardNo,
            ownerId: "P2",
            controllerId: "P2");
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

public enum OpponentBattlefieldCase
{
    None,
    PublicBattlefieldCard,
    OpponentUnitAtBattlefield
}
