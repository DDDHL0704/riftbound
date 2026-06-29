using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldDestinationSourceUnitActiveEntryStaticAbilityTests
{
    private const string ShadowCardNo = "UNL-194/219";
    private const string ShadowObjectId = "P1-SHADOW";
    private const string MainBattlefieldDestination = "BATTLEFIELD:P1-MAIN";

    [Fact]
    public async Task CatalogParsesShadowBattlefieldDestinationSourceUnitEnterReadyStaticAbility()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var spec = Assert.Single(specs, candidate => string.Equals(candidate.CardNo, ShadowCardNo, StringComparison.Ordinal));
        var ability = Assert.Single(
            spec.StaticAbilities,
            candidate => string.Equals(candidate.Kind, StaticAbilityKinds.SourceUnitEnterReady, StringComparison.Ordinal));

        Assert.Equal("如果你将我打出至一处战场，则我以活跃状态进场", ability.Text);
        Assert.True(ability.RequiresBattlefieldDestination);
        Assert.Null(ability.RequiredPlayerExperience);
        Assert.Null(ability.MaxControllerHandCount);
        Assert.Null(ability.RequiredOtherControlledUnitTag);
        Assert.Null(ability.RequiredOpponentControlledBattlefieldCount);
        Assert.Null(ability.RequiredOtherControllerBaseUnitCount);
        Assert.Null(ability.RequiresUnitDestroyedThisTurn);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, ability.Status);
    }

    [Fact]
    public async Task ShadowEntersReadyWhenPlayedToBattlefieldDestination()
    {
        var engine = new CoreRuleEngine();
        var state = BuildShadowState();

        var played = await PlayShadowAsync(engine, state, MainBattlefieldDestination);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State, "shadow-battlefield-destination-ready");

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(ShadowObjectId, resolved.State.PlayerZones["P1"].Battlefields);
        Assert.False(resolved.State.CardObjects[ShadowObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsShadowUnitPlayedToBattlefieldEvent);
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(StaticAbilityKinds.SourceUnitEnterReady, unitEvent.Payload["entryStaticAbilityKind"]);
        Assert.Equal(ShadowObjectId, unitEvent.Payload["entryStaticAbilitySourceObjectId"]);
        Assert.Equal(ShadowCardNo, unitEvent.Payload["entryStaticAbilitySourceCardNo"]);
    }

    [Fact]
    public async Task ShadowDoesNotUseBattlefieldDestinationActiveEntryWhenPlayedToBase()
    {
        var engine = new CoreRuleEngine();
        var state = BuildShadowState();

        var played = await PlayShadowAsync(engine, state, destination: "");
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State, "shadow-base-destination-no-ready");

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(ShadowObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.True(resolved.State.CardObjects[ShadowObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsShadowUnitPlayedToBaseEvent);
        Assert.Equal(true, unitEvent.Payload["isExhausted"]);
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilityKind"));
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilitySourceObjectId"));
    }

    private static async Task<ResolutionResult> PlayShadowAsync(
        CoreRuleEngine engine,
        MatchState state,
        string destination)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-shadow-battlefield-destination-static-entry-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                ShadowObjectId,
                ShadowCardNo,
                [],
                Destination: destination),
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

    private static bool IsShadowUnitPlayedToBattlefieldEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BATTLEFIELD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, ShadowObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, ShadowObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "黑影", StringComparison.Ordinal);
    }

    private static bool IsShadowUnitPlayedToBaseEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, ShadowObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, ShadowObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "黑影", StringComparison.Ordinal);
    }

    private static MatchState BuildShadowState()
    {
        return new MatchState(
            "shadow-battlefield-destination-active-entry-static-ability",
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
                    Hand = [ShadowObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [ShadowObjectId] = new(
                    ShadowObjectId,
                    isExhausted: true,
                    cardNo: ShadowCardNo,
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [ShadowObjectId] = new("P1", "HAND")
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
