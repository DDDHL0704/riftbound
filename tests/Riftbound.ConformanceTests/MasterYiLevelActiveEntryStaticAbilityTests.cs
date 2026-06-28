using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MasterYiLevelActiveEntryStaticAbilityTests
{
    private const string MasterYiLevelLegendCardNo = "UNL-191/219";
    private const string MasterYiLevelLegendObjectId = "P1-LEGEND-MASTER-YI-LEVEL";
    private const string LegionRearguardCardNo = "OGN·010/298";
    private const string LegionRearguardObjectId = "P1-LEGION-REARGUARD";

    [Fact]
    public async Task CatalogParsesMasterYiLevelFriendlyUnitsEnterReadyStaticAbility()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));

        foreach (var cardNo in new[] { "UNL-191/219", "UNL-231/219", "UNL-231*/219" })
        {
            var spec = Assert.Single(specs, candidate => string.Equals(candidate.CardNo, cardNo, StringComparison.Ordinal));
            var ability = Assert.Single(
                spec.StaticAbilities,
                candidate => string.Equals(
                    candidate.Kind,
                    StaticAbilityKinds.FriendlyUnitsEnterReady,
                    StringComparison.Ordinal));

            Assert.Equal("{{等级11>}} 你的单位以活跃状态进场", ability.Text);
            Assert.Equal(11, ability.RequiredPlayerExperience);
            Assert.Equal(BehaviorImplementationStatuses.Implemented, ability.Status);
        }
    }

    [Fact]
    public async Task MasterYiLevelLegendMakesFriendlyUnitsEnterReadyAtElevenExperienceFromStaticAbilitySpec()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLegionRearguardStateWithMasterYiLevelLegend(playerOneExperience: 11);

        var played = await PlayLegionRearguardAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(LegionRearguardObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.False(resolved.State.CardObjects[LegionRearguardObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsLegionRearguardUnitPlayedEvent);
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(StaticAbilityKinds.FriendlyUnitsEnterReady, unitEvent.Payload["entryStaticAbilityKind"]);
        Assert.Equal(MasterYiLevelLegendObjectId, unitEvent.Payload["entryStaticAbilitySourceObjectId"]);
        Assert.Equal(MasterYiLevelLegendCardNo, unitEvent.Payload["entryStaticAbilitySourceCardNo"]);
        Assert.False(unitEvent.Payload.ContainsKey("hasteReadyOptionalCostPaid"));
    }

    [Fact]
    public async Task MasterYiLevelLegendDoesNotMakeFriendlyUnitsEnterReadyBelowElevenExperience()
    {
        var engine = new CoreRuleEngine();
        var state = BuildLegionRearguardStateWithMasterYiLevelLegend(playerOneExperience: 10);

        var played = await PlayLegionRearguardAsync(engine, state);
        Assert.True(played.Accepted, played.ErrorMessage);

        var resolved = await ResolveTopOfStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(LegionRearguardObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.True(resolved.State.CardObjects[LegionRearguardObjectId].IsExhausted);

        var unitEvent = Assert.Single(resolved.Events, IsLegionRearguardUnitPlayedEvent);
        Assert.Equal(true, unitEvent.Payload["isExhausted"]);
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilityKind"));
        Assert.False(unitEvent.Payload.ContainsKey("entryStaticAbilitySourceObjectId"));
    }

    [Fact]
    public void CoreRuleEngineDoesNotKeepMasterYiLevelActiveEntryAsIdentitySpecificBranch()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("MasterYiLevelReadyThreshold", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ControllerHasMasterYiLevelLegend", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("entersActiveFromMasterYiLevel", coreRuleEngineSource, StringComparison.Ordinal);
    }

    private static async Task<ResolutionResult> PlayLegionRearguardAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-master-yi-level-static-entry-play-legion", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                LegionRearguardObjectId,
                LegionRearguardCardNo,
                []),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ResolveTopOfStackAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-master-yi-level-static-entry-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        return await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-master-yi-level-static-entry-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static bool IsLegionRearguardUnitPlayedEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, LegionRearguardObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitObjectId"] as string, LegionRearguardObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["unitName"] as string, "军团后卫", StringComparison.Ordinal);
    }

    private static MatchState BuildLegionRearguardStateWithMasterYiLevelLegend(int playerOneExperience)
    {
        return new MatchState(
            "master-yi-level-active-entry-static-ability",
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
                ["P1"] = new(2, 0),
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
                    Hand = [LegionRearguardObjectId],
                    LegendZone = [MasterYiLevelLegendObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [MasterYiLevelLegendObjectId] = new(
                    MasterYiLevelLegendObjectId,
                    cardNo: MasterYiLevelLegendCardNo,
                    ownerId: "P1",
                    controllerId: "P1"),
                [LegionRearguardObjectId] = new(
                    LegionRearguardObjectId,
                    cardNo: LegionRearguardCardNo,
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [MasterYiLevelLegendObjectId] = new("P1", "LEGEND"),
                [LegionRearguardObjectId] = new("P1", "HAND")
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

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "riftbound-dotnet.sln"))
                || File.Exists(Path.Combine(directory.FullName, "Riftbound.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
