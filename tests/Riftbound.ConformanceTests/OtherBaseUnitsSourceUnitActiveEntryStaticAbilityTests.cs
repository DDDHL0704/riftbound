using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OtherBaseUnitsSourceUnitActiveEntryStaticAbilityTests
{
    private const string XinZhaoCardNo = "SFD·176/221";
    private const string XinZhaoObjectId = "P1-XIN-ZHAO";
    private const string OtherBaseUnitObjectId1 = "P1-OTHER-BASE-UNIT-1";
    private const string OtherBaseUnitObjectId2 = "P1-OTHER-BASE-UNIT-2";
    private const string OpponentBaseUnitObjectId = "P2-OTHER-BASE-UNIT";

    [Fact]
    public async Task CatalogParsesXinZhaoOtherBaseUnitsSourceUnitEnterReadyStaticAbility()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var spec = Assert.Single(specs, candidate => string.Equals(candidate.CardNo, XinZhaoCardNo, StringComparison.Ordinal));
        var ability = Assert.Single(
            spec.StaticAbilities,
            candidate => string.Equals(candidate.Kind, StaticAbilityKinds.SourceUnitEnterReady, StringComparison.Ordinal));

        Assert.Equal("如果你的基地中有不少于两名其他单位，则我以活跃状态进场", ability.Text);
        Assert.Equal(2, ability.RequiredOtherControllerBaseUnitCount);
        Assert.Null(ability.RequiredPlayerExperience);
        Assert.Null(ability.MaxControllerHandCount);
        Assert.Null(ability.RequiredOtherControlledUnitTag);
        Assert.Null(ability.RequiredOpponentControlledBattlefieldCount);
        Assert.Null(ability.RequiresUnitDestroyedThisTurn);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, ability.Status);
    }

    [Fact]
    public async Task XinZhaoEntersReadyWhenControllerBaseHasTwoOtherPublicUnits()
    {
        var engine = new CoreRuleEngine();
        var state = BuildXinZhaoState(OtherBaseUnitCase.TwoFriendlyPublicUnits);

        var played = await PlayXinZhaoAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State, "xin-zhao-two-other-base-units");

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(XinZhaoObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.False(resolved.State.CardObjects[XinZhaoObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsXinZhaoUnitPlayedEvent);
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(StaticAbilityKinds.SourceUnitEnterReady, unitEvent.Payload["entryStaticAbilityKind"]);
        Assert.Equal(XinZhaoObjectId, unitEvent.Payload["entryStaticAbilitySourceObjectId"]);
        Assert.Equal(XinZhaoCardNo, unitEvent.Payload["entryStaticAbilitySourceCardNo"]);
    }

    [Theory]
    [InlineData(OtherBaseUnitCase.OneFriendlyPublicUnit)]
    [InlineData(OtherBaseUnitCase.TwoFriendlyUnitsOneFaceDownStandby)]
    [InlineData(OtherBaseUnitCase.OneFriendlyOneOpponentPublicUnit)]
    public async Task XinZhaoDoesNotEnterReadyWithoutTwoOtherControllerPublicBaseUnits(
        OtherBaseUnitCase otherBaseUnitCase)
    {
        var engine = new CoreRuleEngine();
        var state = BuildXinZhaoState(otherBaseUnitCase);

        var played = await PlayXinZhaoAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State, $"xin-zhao-no-two-other-base-units-{otherBaseUnitCase}");

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(XinZhaoObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.True(resolved.State.CardObjects[XinZhaoObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsXinZhaoUnitPlayedEvent);
        Assert.Equal(true, unitEvent.Payload["isExhausted"]);
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilityKind"));
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilitySourceObjectId"));
    }

    private static async Task<ResolutionResult> PlayXinZhaoAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-xin-zhao-other-base-units-static-entry-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                XinZhaoObjectId,
                XinZhaoCardNo,
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

    private static bool IsXinZhaoUnitPlayedEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, XinZhaoObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, XinZhaoObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "赵信", StringComparison.Ordinal);
    }

    private static MatchState BuildXinZhaoState(OtherBaseUnitCase otherBaseUnitCase)
    {
        var p1Base = new List<string>();
        var p2Base = new List<string>();
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [XinZhaoObjectId] = new(
                XinZhaoObjectId,
                isExhausted: true,
                cardNo: XinZhaoCardNo,
                ownerId: "P1",
                controllerId: "P1")
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [XinZhaoObjectId] = new("P1", "HAND")
        };

        AddFriendlyBaseUnit(OtherBaseUnitObjectId1, faceDownStandby: false);
        if (otherBaseUnitCase is OtherBaseUnitCase.TwoFriendlyPublicUnits)
        {
            AddFriendlyBaseUnit(OtherBaseUnitObjectId2, faceDownStandby: false);
        }
        else if (otherBaseUnitCase is OtherBaseUnitCase.TwoFriendlyUnitsOneFaceDownStandby)
        {
            AddFriendlyBaseUnit(OtherBaseUnitObjectId2, faceDownStandby: true);
        }
        else if (otherBaseUnitCase is OtherBaseUnitCase.OneFriendlyOneOpponentPublicUnit)
        {
            p2Base.Add(OpponentBaseUnitObjectId);
            cardObjects[OpponentBaseUnitObjectId] = BuildBaseUnit(OpponentBaseUnitObjectId, "P2", faceDownStandby: false);
            objectLocations[OpponentBaseUnitObjectId] = new("P2", "BASE");
        }

        return new MatchState(
            "xin-zhao-other-base-units-active-entry-static-ability",
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
                ["P1"] = new(3, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [XinZhaoObjectId],
                    Base = p1Base
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = p2Base
                }
            },
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };

        void AddFriendlyBaseUnit(
            string objectId,
            bool faceDownStandby)
        {
            p1Base.Add(objectId);
            cardObjects[objectId] = BuildBaseUnit(objectId, "P1", faceDownStandby);
            objectLocations[objectId] = new("P1", "BASE");
        }
    }

    private static CardObjectState BuildBaseUnit(
        string objectId,
        string controllerId,
        bool faceDownStandby)
    {
        return new CardObjectState(
            objectId,
            isFaceDown: faceDownStandby,
            isExhausted: true,
            tags: faceDownStandby
                ? [CardObjectTags.UnitCard, CardObjectTags.Standby]
                : [CardObjectTags.UnitCard],
            ownerId: controllerId,
            controllerId: controllerId);
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

    public enum OtherBaseUnitCase
    {
        OneFriendlyPublicUnit,
        TwoFriendlyPublicUnits,
        TwoFriendlyUnitsOneFaceDownStandby,
        OneFriendlyOneOpponentPublicUnit
    }
}
