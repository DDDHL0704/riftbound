using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LegendResourceBridgeVerifierTests
{
    private const string DianaAbilityId = "LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA";
    private const string OrnnAbilityId = "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT";
    private const string KaisaAbilityId = "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL";
    private const string DariusAbilityId = "LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA";

    public static IEnumerable<object[]> SuccessProfiles()
    {
        yield return [new BridgeProfile("Diana", "UNL-197/219", "P1-LEGEND-DIANA", DianaAbilityId, "mana", "spell-duel")];
        yield return [new BridgeProfile("Ornn", "SFD·189/221", "P1-LEGEND-ORNN-SFD-189", OrnnAbilityId, "power", "pending-equipment")];
        yield return [new BridgeProfile("Ornn", "SFD·244/221", "P1-LEGEND-ORNN-SFD-244", OrnnAbilityId, "power", "pending-equipment")];
        yield return [new BridgeProfile("KaiSa", "OGN·247/298", "P1-LEGEND-KAISA-OGN-247", KaisaAbilityId, "power", "pending-spell")];
        yield return [new BridgeProfile("KaiSa", "OGN·299/298", "P1-LEGEND-KAISA-OGN-299", KaisaAbilityId, "power", "pending-spell")];
        yield return [new BridgeProfile("KaiSa", "OGN·299*/298", "P1-LEGEND-KAISA-OGN-299-ALT", KaisaAbilityId, "power", "pending-spell")];
        yield return [new BridgeProfile("Darius", "OGN·253/298", "P1-LEGEND-DARIUS-OGN-253", DariusAbilityId, "mana", "previous-card")];
        yield return [new BridgeProfile("Darius", "OGN·302/298", "P1-LEGEND-DARIUS-OGN-302", DariusAbilityId, "mana", "previous-card")];
        yield return [new BridgeProfile("Darius", "OGN·302*/298", "P1-LEGEND-DARIUS-OGN-302-ALT", DariusAbilityId, "mana", "previous-card")];
    }

    public static IEnumerable<object[]> RejectProfiles()
    {
        yield return [new BridgeProfile("Diana", "UNL-197/219", "P1-LEGEND-DIANA", DianaAbilityId, "mana", "outside-spell-duel")];
        yield return [new BridgeProfile("Ornn", "SFD·189/221", "P1-LEGEND-ORNN-SFD-189", OrnnAbilityId, "power", "wrong-pending-spell")];
        yield return [new BridgeProfile("Ornn", "SFD·244/221", "P1-LEGEND-ORNN-SFD-244", OrnnAbilityId, "power", "wrong-pending-spell")];
        yield return [new BridgeProfile("KaiSa", "OGN·247/298", "P1-LEGEND-KAISA-OGN-247", KaisaAbilityId, "power", "wrong-pending-equipment")];
        yield return [new BridgeProfile("KaiSa", "OGN·299/298", "P1-LEGEND-KAISA-OGN-299", KaisaAbilityId, "power", "wrong-pending-equipment")];
        yield return [new BridgeProfile("KaiSa", "OGN·299*/298", "P1-LEGEND-KAISA-OGN-299-ALT", KaisaAbilityId, "power", "wrong-pending-equipment")];
        yield return [new BridgeProfile("Darius", "OGN·253/298", "P1-LEGEND-DARIUS-OGN-253", DariusAbilityId, "mana", "no-previous-card")];
        yield return [new BridgeProfile("Darius", "OGN·302/298", "P1-LEGEND-DARIUS-OGN-302", DariusAbilityId, "mana", "no-previous-card")];
        yield return [new BridgeProfile("Darius", "OGN·302*/298", "P1-LEGEND-DARIUS-OGN-302-ALT", DariusAbilityId, "mana", "no-previous-card")];
    }

    [Theory]
    [MemberData(nameof(SuccessProfiles))]
    public async Task LegendResourceBridgeSuccessExposesPromptAndGainsOneResource(BridgeProfile profile)
    {
        var state = BuildSuccessState(profile);

        AssertLegendActPrompt(state, profile);

        var result = await ResolveLegendActAsync(state, profile);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.ErrorCode);
        Assert.True(result.State.CardObjects[profile.SourceObjectId].IsExhausted);
        if (string.Equals(profile.ResourceKind, "mana", StringComparison.Ordinal))
        {
            Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        }
        else
        {
            Assert.Equal(new RunePool(0, 1), result.State.RunePools["P1"]);
        }

        AssertResourceGainedEvent(result.Events, profile);
    }

    [Theory]
    [MemberData(nameof(RejectProfiles))]
    public async Task LegendResourceBridgeRejectsWrongGateWithoutMutation(BridgeProfile profile)
    {
        var state = BuildRejectState(profile);
        var initialStateHash = MatchStateHasher.Hash(state);

        var result = await ResolveLegendActAsync(state, profile);

        Assert.False(result.Accepted);
        Assert.Equal(initialStateHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
        Assert.False(result.State.CardObjects[profile.SourceObjectId].IsExhausted);
    }

    private static void AssertLegendActPrompt(MatchState state, BridgeProfile profile)
    {
        var p1Prompt = ResolutionResult.BuildPrompts(state)["P1"];

        Assert.True(p1Prompt.Actionable);
        Assert.Contains(CommandTypes.LegendAct, p1Prompt.Actions);
        var legendCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.LegendAct, StringComparison.Ordinal));
        Assert.True(legendCandidate.Enabled);
        Assert.Contains(
            legendCandidate.Sources ?? [],
            source => string.Equals(source.Id, profile.SourceObjectId, StringComparison.Ordinal));

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(legendCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var requirement = Assert.Single(sourceRequirements, entry =>
            string.Equals(entry["sourceObjectId"] as string, profile.SourceObjectId, StringComparison.Ordinal)
            && string.Equals(entry["cardNo"] as string, profile.CardNo, StringComparison.Ordinal)
            && string.Equals(entry["abilityId"] as string, profile.AbilityId, StringComparison.Ordinal));

        Assert.Equal(0, requirement["minTargetCount"]);
        Assert.Equal(0, requirement["maxTargetCount"]);
        Assert.True(Assert.IsType<bool>(requirement["exhaustsSource"]));
        Assert.True(Assert.IsType<bool>(requirement["resolvesImmediately"]));
    }

    private static void AssertResourceGainedEvent(
        IReadOnlyList<GameEvent> events,
        BridgeProfile profile)
    {
        var eventKind = string.Equals(profile.ResourceKind, "mana", StringComparison.Ordinal)
            ? "MANA_GAINED"
            : "POWER_GAINED";
        var resourceEvent = Assert.Single(events, gameEvent => string.Equals(gameEvent.Kind, eventKind, StringComparison.Ordinal));

        Assert.Equal("P1", resourceEvent.Payload["playerId"]);
        Assert.Equal(profile.SourceObjectId, resourceEvent.Payload["sourceObjectId"]);
        Assert.Equal(profile.AbilityId, resourceEvent.Payload["abilityId"]);
        Assert.True(
            resourceEvent.Payload.TryGetValue("amount", out var amount),
            $"{eventKind} must expose a normalized amount payload field for the legend resource bridge.");
        Assert.Equal(1, amount);
    }

    private static async Task<ResolutionResult> ResolveLegendActAsync(
        MatchState state,
        BridgeProfile profile)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-legend-resource-bridge-{profile.Name}-{profile.CardNo}", "P1", CommandTypes.LegendAct),
            new LegendActCommand(profile.SourceObjectId, profile.AbilityId, [], []),
            CancellationToken.None);
    }

    private static MatchState BuildSuccessState(BridgeProfile profile)
    {
        return profile.Gate switch
        {
            "spell-duel" => LegendSpellDuelFocusState(profile.CardNo, profile.SourceObjectId),
            "pending-equipment" => LegendPriorityWindowState(profile.CardNo, profile.SourceObjectId, CardObjectTags.EquipmentCard),
            "pending-spell" => LegendPriorityWindowState(profile.CardNo, profile.SourceObjectId, CardObjectTags.SpellCard),
            "previous-card" => LegendActiveAbilityState(profile.CardNo, profile.SourceObjectId, mana: 0) with
            {
                PlayerCardsPlayedThisTurn = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["P1"] = 1
                }
            },
            _ => throw new InvalidOperationException($"Unknown success gate: {profile.Gate}")
        };
    }

    private static MatchState BuildRejectState(BridgeProfile profile)
    {
        return profile.Gate switch
        {
            "outside-spell-duel" => LegendActiveAbilityState(profile.CardNo, profile.SourceObjectId, mana: 0),
            "wrong-pending-spell" => LegendPriorityWindowState(profile.CardNo, profile.SourceObjectId, CardObjectTags.SpellCard),
            "wrong-pending-equipment" => LegendPriorityWindowState(profile.CardNo, profile.SourceObjectId, CardObjectTags.EquipmentCard),
            "no-previous-card" => LegendActiveAbilityState(profile.CardNo, profile.SourceObjectId, mana: 0),
            _ => throw new InvalidOperationException($"Unknown reject gate: {profile.Gate}")
        };
    }

    private static MatchState LegendActiveAbilityState(string sourceCardNo, string sourceObjectId, int mana)
    {
        return new MatchState(
            "legend-resource-bridge-room",
            0,
            906,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-LEGEND-BASE-UNIT"],
                    Battlefields = ["P1-LEGEND-BATTLEFIELD-UNIT"],
                    LegendZone = [sourceObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: sourceCardNo,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-BASE-UNIT"] = new(
                    "P1-LEGEND-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-BATTLEFIELD-UNIT"] = new(
                    "P1-LEGEND-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });
    }

    private static MatchState LegendSpellDuelFocusState(string sourceCardNo, string sourceObjectId)
    {
        return LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana: 0) with
        {
            TimingState = TimingStates.SpellDuelOpen,
            FocusPlayerId = "P1",
            PassedFocusPlayerIds = [],
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            }
        };
    }

    private static MatchState LegendPriorityWindowState(
        string sourceCardNo,
        string sourceObjectId,
        string pendingSourceTag)
    {
        var state = LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana: 0);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P2-PENDING-SOURCE"] = new(
            "P2-PENDING-SOURCE",
            cardNo: string.Equals(pendingSourceTag, CardObjectTags.EquipmentCard, StringComparison.Ordinal)
                ? "SFD·123/221"
                : "UNL-159/219",
            tags: [pendingSourceTag],
            ownerId: "P2",
            controllerId: "P2");

        return state with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            PassedPriorityPlayerIds = [],
            StackItems =
            [
                new StackItemState(
                    "STACK-PENDING-LEGEND-RESOURCE",
                    "P2",
                    "P2-PENDING-SOURCE",
                    "PENDING_LEGEND_RESOURCE_TEST",
                    cardObjects["P2-PENDING-SOURCE"].CardNo,
                    [],
                    0)
            ],
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            CardObjects = cardObjects
        };
    }

    public sealed record BridgeProfile(
        string Name,
        string CardNo,
        string SourceObjectId,
        string AbilityId,
        string ResourceKind,
        string Gate);
}
