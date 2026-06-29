using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class UnconditionalSourceUnitActiveEntryStaticAbilityTests
{
    private const string AggressiveDragonhoundCardNo = "SFD·006/221";
    private const string AggressiveDragonhoundObjectId = "P1-AGGRESSIVE-DRAGONHOUND";
    private const string VanguardSquireCardNo = "OGS·016/024";
    private const string HasteReminderOnlyCardNo = "UNL-006/219";

    [Theory]
    [InlineData(AggressiveDragonhoundCardNo)]
    [InlineData(VanguardSquireCardNo)]
    public async Task CatalogParsesUnconditionalSourceUnitEnterReadyStaticAbility(string cardNo)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var spec = Assert.Single(specs, candidate => string.Equals(candidate.CardNo, cardNo, StringComparison.Ordinal));
        var ability = Assert.Single(
            spec.StaticAbilities,
            candidate => string.Equals(candidate.Kind, StaticAbilityKinds.SourceUnitEnterReady, StringComparison.Ordinal));

        Assert.Equal("我以活跃状态进场", ability.Text);
        Assert.Null(ability.RequiredPlayerExperience);
        Assert.Null(ability.MaxControllerHandCount);
        Assert.Null(ability.RequiredOtherControlledUnitTag);
        Assert.Equal(BehaviorImplementationStatuses.Implemented, ability.Status);
    }

    [Fact]
    public async Task CatalogDoesNotTreatHasteReminderTextAsUnconditionalSourceUnitEnterReady()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        var spec = Assert.Single(
            specs,
            candidate => string.Equals(candidate.CardNo, HasteReminderOnlyCardNo, StringComparison.Ordinal));

        Assert.DoesNotContain(
            spec.StaticAbilities,
            candidate => string.Equals(candidate.Kind, StaticAbilityKinds.SourceUnitEnterReady, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AggressiveDragonhoundEntersReadyFromUnconditionalSourceUnitStaticAbilitySpec()
    {
        var engine = new CoreRuleEngine();
        var state = BuildAggressiveDragonhoundState();

        var played = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-aggressive-dragonhound-unconditional-static-entry-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                AggressiveDragonhoundObjectId,
                AggressiveDragonhoundCardNo,
                []),
            CancellationToken.None);
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-aggressive-dragonhound-unconditional-static-entry-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var resolved = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-aggressive-dragonhound-unconditional-static-entry-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(AggressiveDragonhoundObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.False(resolved.State.CardObjects[AggressiveDragonhoundObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsAggressiveDragonhoundUnitPlayedEvent);
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(StaticAbilityKinds.SourceUnitEnterReady, unitEvent.Payload["entryStaticAbilityKind"]);
        Assert.Equal(AggressiveDragonhoundObjectId, unitEvent.Payload["entryStaticAbilitySourceObjectId"]);
        Assert.Equal(AggressiveDragonhoundCardNo, unitEvent.Payload["entryStaticAbilitySourceCardNo"]);
    }

    private static bool IsAggressiveDragonhoundUnitPlayedEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, AggressiveDragonhoundObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, AggressiveDragonhoundObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "好斗的龙犬", StringComparison.Ordinal);
    }

    private static MatchState BuildAggressiveDragonhoundState()
    {
        return new MatchState(
            "aggressive-dragonhound-unconditional-active-entry-static-ability",
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
                    Hand = [AggressiveDragonhoundObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [AggressiveDragonhoundObjectId] = new(
                    AggressiveDragonhoundObjectId,
                    isExhausted: true,
                    cardNo: AggressiveDragonhoundCardNo,
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [AggressiveDragonhoundObjectId] = new("P1", "HAND")
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
