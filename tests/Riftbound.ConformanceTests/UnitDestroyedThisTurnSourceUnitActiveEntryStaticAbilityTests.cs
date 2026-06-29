using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class UnitDestroyedThisTurnSourceUnitActiveEntryStaticAbilityTests
{
    private const string JungleElephantCardNo = "UNL-008/219";
    private const string JungleElephantObjectId = "P1-JUNGLE-ELEPHANT";

    [Fact]
    public async Task CatalogParsesJungleElephantUnitDestroyedThisTurnSourceUnitEnterReadyStaticAbility()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var spec = Assert.Single(specs, candidate => string.Equals(candidate.CardNo, JungleElephantCardNo, StringComparison.Ordinal));
        var ability = Assert.Single(
            spec.StaticAbilities,
            candidate => string.Equals(candidate.Kind, StaticAbilityKinds.SourceUnitEnterReady, StringComparison.Ordinal));

        Assert.Equal("如果本回合内有单位被摧毁，则我以活跃状态进场", ability.Text);
        Assert.True(ability.RequiresUnitDestroyedThisTurn);
        Assert.Null(ability.RequiredPlayerExperience);
        Assert.Null(ability.MaxControllerHandCount);
        Assert.Null(ability.RequiredOtherControlledUnitTag);
        Assert.Null(ability.RequiredOpponentControlledBattlefieldCount);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, ability.Status);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task JungleElephantEntersReadyWhenAnyUnitWasDestroyedThisTurn(
        string destroyedUnitOwnerId)
    {
        var engine = new CoreRuleEngine();
        var state = BuildJungleElephantState([destroyedUnitOwnerId]);

        var played = await PlayJungleElephantAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State, $"jungle-elephant-destroyed-{destroyedUnitOwnerId}");

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(JungleElephantObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.False(resolved.State.CardObjects[JungleElephantObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsJungleElephantUnitPlayedEvent);
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(StaticAbilityKinds.SourceUnitEnterReady, unitEvent.Payload["entryStaticAbilityKind"]);
        Assert.Equal(JungleElephantObjectId, unitEvent.Payload["entryStaticAbilitySourceObjectId"]);
        Assert.Equal(JungleElephantCardNo, unitEvent.Payload["entryStaticAbilitySourceCardNo"]);
    }

    [Fact]
    public async Task JungleElephantDoesNotEnterReadyWhenNoUnitWasDestroyedThisTurn()
    {
        var engine = new CoreRuleEngine();
        var state = BuildJungleElephantState([]);

        var played = await PlayJungleElephantAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State, "jungle-elephant-no-destroyed-unit");

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(JungleElephantObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.True(resolved.State.CardObjects[JungleElephantObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsJungleElephantUnitPlayedEvent);
        Assert.Equal(true, unitEvent.Payload["isExhausted"]);
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilityKind"));
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilitySourceObjectId"));
    }

    private static async Task<ResolutionResult> PlayJungleElephantAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-jungle-elephant-destroyed-unit-static-entry-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                JungleElephantObjectId,
                JungleElephantCardNo,
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

    private static bool IsJungleElephantUnitPlayedEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, JungleElephantObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, JungleElephantObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "莽林巨象", StringComparison.Ordinal);
    }

    private static MatchState BuildJungleElephantState(
        IReadOnlyList<string> destroyedUnitOwnerIdsThisTurn)
    {
        return new MatchState(
            "jungle-elephant-destroyed-unit-active-entry-static-ability",
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
                ["P1"] = new(6, 0),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [JungleElephantObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [JungleElephantObjectId] = new(
                    JungleElephantObjectId,
                    isExhausted: true,
                    cardNo: JungleElephantCardNo,
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [JungleElephantObjectId] = new("P1", "HAND")
            },
            DestroyedUnitOwnerIdsThisTurn = destroyedUnitOwnerIdsThisTurn
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
