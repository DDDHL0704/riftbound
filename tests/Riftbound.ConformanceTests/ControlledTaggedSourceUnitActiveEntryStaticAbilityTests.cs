using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ControlledTaggedSourceUnitActiveEntryStaticAbilityTests
{
    private const string FiercewingCardNo = "SFD·094/221";
    private const string FiercewingObjectId = "P1-FIERCEWING";
    private const string SpeedingMechCardNo = "SFD·071/221";
    private const string OtherDragonObjectId = "P1-OTHER-DRAGON";
    private const string OpponentDragonObjectId = "P2-OTHER-DRAGON";

    [Fact]
    public async Task CatalogParsesFiercewingOtherControlledDragonSourceUnitEnterReadyStaticAbility()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var spec = Assert.Single(specs, candidate => string.Equals(candidate.CardNo, FiercewingCardNo, StringComparison.Ordinal));
        var ability = Assert.Single(
            spec.StaticAbilities,
            candidate => string.Equals(candidate.Kind, StaticAbilityKinds.SourceUnitEnterReady, StringComparison.Ordinal));

        Assert.Equal("如果你控制着其他“龙”属性单位，则我以活跃状态进场", ability.Text);
        Assert.Equal("龙", ability.RequiredOtherControlledUnitTag);
        Assert.Null(ability.RequiredPlayerExperience);
        Assert.Null(ability.MaxControllerHandCount);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, ability.Status);
    }

    [Fact]
    public async Task CatalogParsesSpeedingMechOtherControlledMechanicalSourceUnitEnterReadyStaticAbility()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var spec = Assert.Single(specs, candidate => string.Equals(candidate.CardNo, SpeedingMechCardNo, StringComparison.Ordinal));
        var ability = Assert.Single(
            spec.StaticAbilities,
            candidate => string.Equals(candidate.Kind, StaticAbilityKinds.SourceUnitEnterReady, StringComparison.Ordinal));

        Assert.Equal("如果你控制着其他“机械”单位，则我以活跃状态进场", ability.Text);
        Assert.Equal("机械", ability.RequiredOtherControlledUnitTag);
        Assert.Null(ability.RequiredPlayerExperience);
        Assert.Null(ability.MaxControllerHandCount);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, ability.Status);
    }

    [Fact]
    public async Task FiercewingEntersReadyWhenControllerHasOtherFaceUpDragonUnit()
    {
        var engine = new CoreRuleEngine();
        var state = BuildFiercewingState(ControlledTaggedUnitCase.FriendlyFaceUpDragon);

        var played = await PlayFiercewingAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State, "fiercewing-controlled-dragon-ready");

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(FiercewingObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.False(resolved.State.CardObjects[FiercewingObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsFiercewingUnitPlayedEvent);
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(StaticAbilityKinds.SourceUnitEnterReady, unitEvent.Payload["entryStaticAbilityKind"]);
        Assert.Equal(FiercewingObjectId, unitEvent.Payload["entryStaticAbilitySourceObjectId"]);
        Assert.Equal(FiercewingCardNo, unitEvent.Payload["entryStaticAbilitySourceCardNo"]);
    }

    [Theory]
    [InlineData(ControlledTaggedUnitCase.None)]
    [InlineData(ControlledTaggedUnitCase.FriendlyFaceDownStandbyDragon)]
    [InlineData(ControlledTaggedUnitCase.OpponentFaceUpDragon)]
    public async Task FiercewingDoesNotEnterReadyWithoutOtherControlledPublicDragonUnit(
        ControlledTaggedUnitCase controlledTaggedUnitCase)
    {
        var engine = new CoreRuleEngine();
        var state = BuildFiercewingState(controlledTaggedUnitCase);

        var played = await PlayFiercewingAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State, $"fiercewing-no-controlled-dragon-{controlledTaggedUnitCase}");

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(FiercewingObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.True(resolved.State.CardObjects[FiercewingObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsFiercewingUnitPlayedEvent);
        Assert.Equal(true, unitEvent.Payload["isExhausted"]);
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilityKind"));
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilitySourceObjectId"));
    }

    private static async Task<ResolutionResult> PlayFiercewingAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-fiercewing-controlled-tag-static-entry-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                FiercewingObjectId,
                FiercewingCardNo,
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

    private static bool IsFiercewingUnitPlayedEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FiercewingObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, FiercewingObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "凶翼", StringComparison.Ordinal);
    }

    private static MatchState BuildFiercewingState(ControlledTaggedUnitCase controlledTaggedUnitCase)
    {
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                Hand = [FiercewingObjectId]
            },
            ["P2"] = PlayerZones.Empty
        };
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [FiercewingObjectId] = new(
                FiercewingObjectId,
                isExhausted: true,
                cardNo: FiercewingCardNo,
                ownerId: "P1",
                controllerId: "P1")
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [FiercewingObjectId] = new("P1", "HAND")
        };

        if (controlledTaggedUnitCase is ControlledTaggedUnitCase.FriendlyFaceUpDragon
            or ControlledTaggedUnitCase.FriendlyFaceDownStandbyDragon)
        {
            var faceDownStandby = controlledTaggedUnitCase is ControlledTaggedUnitCase.FriendlyFaceDownStandbyDragon;
            playerZones["P1"] = playerZones["P1"] with
            {
                Base = [OtherDragonObjectId]
            };
            cardObjects[OtherDragonObjectId] = BuildDragonObject(OtherDragonObjectId, "P1", faceDownStandby);
            objectLocations[OtherDragonObjectId] = new("P1", "BASE");
        }
        else if (controlledTaggedUnitCase is ControlledTaggedUnitCase.OpponentFaceUpDragon)
        {
            playerZones["P2"] = playerZones["P2"] with
            {
                Base = [OpponentDragonObjectId]
            };
            cardObjects[OpponentDragonObjectId] = BuildDragonObject(OpponentDragonObjectId, "P2", faceDownStandby: false);
            objectLocations[OpponentDragonObjectId] = new("P2", "BASE");
        }

        return new MatchState(
            "fiercewing-controlled-tag-active-entry-static-ability",
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
                ["P1"] = new(7, 0),
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

    private static CardObjectState BuildDragonObject(
        string objectId,
        string controllerId,
        bool faceDownStandby)
    {
        return new CardObjectState(
            objectId,
            isFaceDown: faceDownStandby,
            isExhausted: true,
            tags: faceDownStandby
                ? [CardObjectTags.UnitCard, CardObjectTags.Standby, "龙"]
                : [CardObjectTags.UnitCard, "龙"],
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
}

public enum ControlledTaggedUnitCase
{
    None,
    FriendlyFaceUpDragon,
    FriendlyFaceDownStandbyDragon,
    OpponentFaceUpDragon
}
