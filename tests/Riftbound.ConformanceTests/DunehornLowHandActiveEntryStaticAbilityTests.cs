using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DunehornLowHandActiveEntryStaticAbilityTests
{
    private const string DunehornBeastCardNo = "SFD·027/221";
    private const string DunehornBeastObjectId = "P1-DUNEHORN-BEAST";

    [Fact]
    public async Task CatalogParsesDunehornLowHandSourceUnitEnterReadyStaticAbility()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var spec = Assert.Single(specs, candidate => string.Equals(candidate.CardNo, DunehornBeastCardNo, StringComparison.Ordinal));
        var ability = Assert.Single(
            spec.StaticAbilities,
            candidate => string.Equals(candidate.Kind, StaticAbilityKinds.SourceUnitEnterReady, StringComparison.Ordinal));

        Assert.Equal("如果你的手牌不超过两张，则我以活跃状态进场", ability.Text);
        Assert.Equal(2, ability.MaxControllerHandCount);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, ability.Status);
    }

    [Fact]
    public async Task DunehornEntersReadyWhenControllerHasAtMostTwoCardsInHandAfterPlay()
    {
        var engine = new CoreRuleEngine();
        var state = BuildDunehornState(remainingHandCountAfterPlay: 2);

        var played = await PlayDunehornAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(DunehornBeastObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.False(resolved.State.CardObjects[DunehornBeastObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsDunehornUnitPlayedEvent);
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(StaticAbilityKinds.SourceUnitEnterReady, unitEvent.Payload["entryStaticAbilityKind"]);
        Assert.Equal(DunehornBeastObjectId, unitEvent.Payload["entryStaticAbilitySourceObjectId"]);
        Assert.Equal(DunehornBeastCardNo, unitEvent.Payload["entryStaticAbilitySourceCardNo"]);
    }

    [Fact]
    public async Task DunehornDoesNotEnterReadyWhenControllerHasMoreThanTwoCardsInHandAfterPlay()
    {
        var engine = new CoreRuleEngine();
        var state = BuildDunehornState(remainingHandCountAfterPlay: 3);

        var played = await PlayDunehornAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(DunehornBeastObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.True(resolved.State.CardObjects[DunehornBeastObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsDunehornUnitPlayedEvent);
        Assert.Equal(true, unitEvent.Payload["isExhausted"]);
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilityKind"));
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilitySourceObjectId"));
    }

    private static async Task<ResolutionResult> PlayDunehornAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-dunehorn-low-hand-static-entry-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                DunehornBeastObjectId,
                DunehornBeastCardNo,
                []),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ResolveTopOfStackAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-dunehorn-low-hand-static-entry-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        return await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-dunehorn-low-hand-static-entry-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static bool IsDunehornUnitPlayedEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, DunehornBeastObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, DunehornBeastObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "穿沙角兽", StringComparison.Ordinal);
    }

    private static MatchState BuildDunehornState(int remainingHandCountAfterPlay)
    {
        var fillerHandObjectIds = Enumerable
            .Range(1, remainingHandCountAfterPlay)
            .Select(index => $"P1-HAND-FILLER-{index:000}")
            .ToArray();
        var handObjectIds = new[] { DunehornBeastObjectId }
            .Concat(fillerHandObjectIds)
            .ToArray();
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [DunehornBeastObjectId] = new(
                DunehornBeastObjectId,
                isExhausted: true,
                cardNo: DunehornBeastCardNo,
                ownerId: "P1",
                controllerId: "P1")
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [DunehornBeastObjectId] = new("P1", "HAND")
        };
        foreach (var fillerObjectId in fillerHandObjectIds)
        {
            cardObjects[fillerObjectId] = new(
                fillerObjectId,
                cardNo: "SFD·006/221",
                ownerId: "P1",
                controllerId: "P1");
            objectLocations[fillerObjectId] = new("P1", "HAND");
        }

        return new MatchState(
            "dunehorn-low-hand-active-entry-static-ability",
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
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = handObjectIds
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
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
