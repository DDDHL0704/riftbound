using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SourceUnitLevelActiveEntryStaticAbilityTests
{
    private const string FlameclawCardNo = "UNL-016/219";
    private const string FlameclawObjectId = "P1-FLAMECLAW";

    [Fact]
    public async Task CatalogParsesFlameclawLevelSourceUnitEnterReadyStaticAbility()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var spec = Assert.Single(specs, candidate => string.Equals(candidate.CardNo, FlameclawCardNo, StringComparison.Ordinal));
        var ability = Assert.Single(
            spec.StaticAbilities,
            candidate => string.Equals(candidate.Kind, StaticAbilityKinds.SourceUnitEnterReady, StringComparison.Ordinal));

        Assert.Contains("{{等级3>}} 我获得{{S}}+1，并以活跃状态进场", ability.Text, StringComparison.Ordinal);
        Assert.Equal(3, ability.RequiredPlayerExperience);
        Assert.Null(ability.MaxControllerHandCount);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, ability.Status);
    }

    [Fact]
    public async Task FlameclawEntersReadyAtLevelThreeFromSourceUnitStaticAbilitySpec()
    {
        var engine = new CoreRuleEngine();
        var state = BuildFlameclawState(playerOneExperience: 3);

        var played = await PlayFlameclawAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(FlameclawObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.False(resolved.State.CardObjects[FlameclawObjectId].IsExhausted);
        Assert.Equal(3, resolved.State.CardObjects[FlameclawObjectId].Power);

        var staticAura = Assert.Single(
            resolved.State.ContinuousEffects,
            effect => string.Equals(effect.Layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, FlameclawObjectId, StringComparison.Ordinal)
                && string.Equals(effect.TargetObjectId, FlameclawObjectId, StringComparison.Ordinal));
        Assert.Equal(StaticAuraKinds.SourceObjectPower, staticAura.EffectKind);
        Assert.Equal(1, staticAura.PowerDelta);
        Assert.Equal(3, staticAura.BasePower);
        Assert.Equal(4, staticAura.EffectivePower);

        var unitEvent = Assert.Single(resolved.Events, IsFlameclawUnitPlayedEvent);
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(3, unitEvent.Payload["power"]);
        Assert.Equal(StaticAbilityKinds.SourceUnitEnterReady, unitEvent.Payload["entryStaticAbilityKind"]);
        Assert.Equal(FlameclawObjectId, unitEvent.Payload["entryStaticAbilitySourceObjectId"]);
        Assert.Equal(FlameclawCardNo, unitEvent.Payload["entryStaticAbilitySourceCardNo"]);
    }

    [Fact]
    public async Task FlameclawDoesNotEnterReadyBelowLevelThree()
    {
        var engine = new CoreRuleEngine();
        var state = BuildFlameclawState(playerOneExperience: 2);

        var played = await PlayFlameclawAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(FlameclawObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.True(resolved.State.CardObjects[FlameclawObjectId].IsExhausted);

        Assert.DoesNotContain(
            resolved.State.ContinuousEffects,
            effect => string.Equals(effect.EffectKind, StaticAuraKinds.SourceObjectPower, StringComparison.Ordinal)
                && string.Equals(effect.SourceObjectId, FlameclawObjectId, StringComparison.Ordinal));

        var unitEvent = Assert.Single(resolved.Events, IsFlameclawUnitPlayedEvent);
        Assert.Equal(true, unitEvent.Payload["isExhausted"]);
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilityKind"));
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilitySourceObjectId"));
    }

    private static async Task<ResolutionResult> PlayFlameclawAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-flameclaw-level-static-entry-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                FlameclawObjectId,
                FlameclawCardNo,
                []),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ResolveTopOfStackAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-flameclaw-level-static-entry-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        return await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-flameclaw-level-static-entry-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static bool IsFlameclawUnitPlayedEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, FlameclawObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, FlameclawObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "焰爪", StringComparison.Ordinal);
    }

    private static MatchState BuildFlameclawState(int playerOneExperience)
    {
        return new MatchState(
            "flameclaw-level-active-entry-static-ability",
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
                ["P1"] = playerOneExperience,
                ["P2"] = 0
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [FlameclawObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [FlameclawObjectId] = new(
                    FlameclawObjectId,
                    isExhausted: true,
                    cardNo: FlameclawCardNo,
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [FlameclawObjectId] = new("P1", "HAND")
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
