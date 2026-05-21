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

    public static IEnumerable<object[]> ManaSuccessProfiles()
    {
        foreach (var row in SuccessProfiles())
        {
            var profile = Assert.IsType<BridgeProfile>(row[0]);
            if (string.Equals(profile.ResourceKind, "mana", StringComparison.Ordinal))
            {
                yield return row;
            }
        }
    }

    public static IEnumerable<object[]> PowerSuccessProfiles()
    {
        foreach (var row in SuccessProfiles())
        {
            var profile = Assert.IsType<BridgeProfile>(row[0]);
            if (string.Equals(profile.ResourceKind, "power", StringComparison.Ordinal))
            {
                yield return row;
            }
        }
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
    [MemberData(nameof(ManaSuccessProfiles))]
    public async Task LegendResourceBridgeGeneratedManaCanPayLaterLegalManaCost(BridgeProfile profile)
    {
        var gained = await ResolveLegendActAsync(BuildSuccessState(profile), profile);
        Assert.True(gained.Accepted, gained.ErrorMessage);

        var pendingPayment = new PendingPaymentState(
            $"LEGEND-RESOURCE-MANA-PAY-{profile.SourceObjectId}",
            "LEGEND_RESOURCE_PAYMENT",
            "P1",
            manaCost: 1,
            legalPaymentChoiceIds: ["SPEND_MANA:1"],
            reason: "LEGEND_GENERATED_MANA_CONSUMPTION");
        var paymentState = gained.State with
        {
            PendingPayment = pendingPayment
        };

        var paid = await new CoreRuleEngine().ResolveAsync(
            paymentState,
            new PlayerIntent($"intent-legend-resource-bridge-mana-consume-{profile.Name}-{profile.CardNo}", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(RunePool.Empty, paid.State.RunePools["P1"]);
        var costEvent = Assert.Single(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(1, costEvent.Payload["totalManaCost"]);
        Assert.Equal(0, costEvent.Payload["totalPowerCost"]);
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
        Assert.Equal(["SPEND_MANA:1"], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(PowerSuccessProfiles))]
    public async Task LegendResourceBridgeGeneratedPowerCanPayLaterLegalPowerCost(BridgeProfile profile)
    {
        var gained = await ResolveLegendActAsync(BuildSuccessState(profile), profile);
        Assert.True(gained.Accepted, gained.ErrorMessage);

        var pendingPayment = new PendingPaymentState(
            $"LEGEND-RESOURCE-POWER-PAY-{profile.SourceObjectId}",
            "LEGEND_RESOURCE_PAYMENT",
            "P1",
            powerCost: 1,
            legalPaymentChoiceIds: ["SPEND_POWER:1"],
            reason: "LEGEND_GENERATED_POWER_CONSUMPTION");
        var paymentState = gained.State with
        {
            PendingPayment = pendingPayment
        };

        var paid = await new CoreRuleEngine().ResolveAsync(
            paymentState,
            new PlayerIntent($"intent-legend-resource-bridge-power-consume-{profile.Name}-{profile.CardNo}", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, ["SPEND_POWER:1"]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(RunePool.Empty, paid.State.RunePools["P1"]);
        var costEvent = Assert.Single(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(0, costEvent.Payload["totalManaCost"]);
        Assert.Equal(1, costEvent.Payload["totalPowerCost"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
        Assert.Equal(["SPEND_POWER:1"], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(SuccessProfiles))]
    public async Task LegendResourceBridgeGeneratedResourceUsesRunePoolEndTurnCleanupLifecycle(BridgeProfile profile)
    {
        var gained = await ResolveLegendActAsync(BuildSuccessState(profile), profile);
        Assert.True(gained.Accepted, gained.ErrorMessage);

        var cleanupState = MoveToOpenMainForEndTurn(gained.State);
        var cleanup = await new CoreRuleEngine().ResolveAsync(
            cleanupState,
            new PlayerIntent($"intent-legend-resource-bridge-cleanup-{profile.Name}-{profile.CardNo}", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(cleanup.Accepted, cleanup.ErrorMessage);
        Assert.Equal(RunePool.Empty, cleanup.State.RunePools["P1"]);
        Assert.Contains(
            cleanup.Events,
            gameEvent => string.Equals(gameEvent.Kind, "RUNE_POOL_CLEARED", StringComparison.Ordinal)
                && Assert.IsAssignableFrom<IEnumerable<string>>(gameEvent.Payload["playerIds"]).Contains("P1"));
    }

    [Theory]
    [MemberData(nameof(RejectProfiles))]
    public async Task LegendResourceBridgeRejectsWrongGateWithoutMutation(BridgeProfile profile)
    {
        var state = BuildRejectState(profile);
        var initialStateHash = MatchStateHasher.Hash(state);

        var result = await ResolveLegendActAsync(state, profile);

        AssertRejectedNoMutation(result, initialStateHash);
        Assert.False(result.State.CardObjects[profile.SourceObjectId].IsExhausted);
    }

    [Theory]
    [MemberData(nameof(RejectProfiles))]
    public void LegendResourceBridgePromptFiltersIllegalSourceAtWrongGate(BridgeProfile profile)
    {
        var state = BuildRejectState(profile);
        var p1Prompt = ResolutionResult.BuildPrompts(state)["P1"];

        Assert.DoesNotContain(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.LegendAct, StringComparison.Ordinal)
                && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, profile.SourceObjectId, StringComparison.Ordinal)));
    }

    [Theory]
    [MemberData(nameof(SuccessProfiles))]
    public async Task LegendResourceBridgeRejectsStaleSourceWithoutMutation(BridgeProfile profile)
    {
        var state = BuildSuccessState(profile);
        var zones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        zones["P1"] = zones["P1"] with
        {
            LegendZone = []
        };
        state = state with
        {
            PlayerZones = zones
        };
        var initialStateHash = MatchStateHasher.Hash(state);

        var result = await ResolveLegendActAsync(state, profile);

        AssertRejectedNoMutation(result, initialStateHash);
    }

    [Theory]
    [MemberData(nameof(SuccessProfiles))]
    public async Task LegendResourceBridgeRejectsExhaustedSourceAndDuplicateUseWithoutMutation(BridgeProfile profile)
    {
        var state = ExhaustSource(BuildSuccessState(profile), profile.SourceObjectId);
        var initialStateHash = MatchStateHasher.Hash(state);

        var exhausted = await ResolveLegendActAsync(state, profile);

        AssertRejectedNoMutation(exhausted, initialStateHash);

        var gained = await ResolveLegendActAsync(BuildSuccessState(profile), profile);
        Assert.True(gained.Accepted, gained.ErrorMessage);
        var afterFirstUseHash = MatchStateHasher.Hash(gained.State);

        var duplicate = await ResolveLegendActAsync(gained.State, profile);

        AssertRejectedNoMutation(duplicate, afterFirstUseHash);
        Assert.Equal(new RunePool(
            string.Equals(profile.ResourceKind, "mana", StringComparison.Ordinal) ? 1 : 0,
            string.Equals(profile.ResourceKind, "power", StringComparison.Ordinal) ? 1 : 0),
            duplicate.State.RunePools["P1"]);
    }

    [Theory]
    [MemberData(nameof(SuccessProfiles))]
    public async Task LegendResourceBridgeRejectsAcceptedCommandReplayWithoutMutation(BridgeProfile profile)
    {
        var engine = new CoreRuleEngine();
        var state = BuildSuccessState(profile);
        var command = new LegendActCommand(profile.SourceObjectId, profile.AbilityId, [], []);

        AssertLegendActPrompt(state, profile);

        var gained = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-legend-resource-bridge-replay-first-{profile.Name}-{profile.CardNo}", "P1", CommandTypes.LegendAct),
            command,
            CancellationToken.None);

        Assert.True(gained.Accepted, gained.ErrorMessage);
        Assert.True(gained.State.CardObjects[profile.SourceObjectId].IsExhausted);
        Assert.Equal(1, CountEvents(gained.Events, ResourceEventKind(profile)));
        Assert.Equal(0, CountEvents(gained.Events, "COST_PAID"));
        Assert.Null(gained.State.PendingPayment);
        Assert.Equal(state.PendingTaskQueue.Phase, gained.State.PendingTaskQueue.Phase);
        Assert.Equal(state.PendingTaskQueue.ActiveTaskId, gained.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(state.PendingTaskQueue.Tasks, gained.State.PendingTaskQueue.Tasks);
        AssertNoLegendActPromptForSource(gained.State, profile);
        var postGainHash = MatchStateHasher.Hash(gained.State);

        var replay = await engine.ResolveAsync(
            gained.State,
            new PlayerIntent($"intent-legend-resource-bridge-replay-second-{profile.Name}-{profile.CardNo}", "P1", CommandTypes.LegendAct),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Empty(replay.Events);
        Assert.Equal(postGainHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(gained.State.RunePools["P1"], replay.State.RunePools["P1"]);
        Assert.Equal(gained.State.StackItems, replay.State.StackItems);
        Assert.Equal(gained.State.PendingTaskQueue.Phase, replay.State.PendingTaskQueue.Phase);
        Assert.Equal(gained.State.PendingTaskQueue.ActiveTaskId, replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(gained.State.PendingTaskQueue.Tasks, replay.State.PendingTaskQueue.Tasks);
        Assert.Null(replay.State.PendingPayment);
        Assert.True(replay.State.CardObjects[profile.SourceObjectId].IsExhausted);
        AssertNoLegendActPromptForSource(replay.State, profile);
        Assert.Equal(0, CountEvents(replay.Events, ResourceEventKind(profile)));
        Assert.Equal(0, CountEvents(replay.Events, "COST_PAID"));
    }

    [Theory]
    [MemberData(nameof(SuccessProfiles))]
    public async Task LegendResourceBridgeRejectsHandwrittenIllegalAbilityWithoutMutation(BridgeProfile profile)
    {
        var state = BuildSuccessState(profile);
        var initialStateHash = MatchStateHasher.Hash(state);
        var illegalAbilityId = profile.AbilityId switch
        {
            DianaAbilityId => OrnnAbilityId,
            _ => DianaAbilityId
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-legend-resource-bridge-illegal-ability-{profile.Name}-{profile.CardNo}", "P1", CommandTypes.LegendAct),
            new LegendActCommand(profile.SourceObjectId, illegalAbilityId, [], []),
            CancellationToken.None);

        AssertRejectedNoMutation(result, initialStateHash);
    }

    [Theory]
    [MemberData(nameof(ManaSuccessProfiles))]
    public async Task LegendResourceBridgeGeneratedManaCannotBeSpentTwice(BridgeProfile profile)
    {
        var gained = await ResolveLegendActAsync(BuildSuccessState(profile), profile);
        Assert.True(gained.Accepted, gained.ErrorMessage);
        var pendingPayment = new PendingPaymentState(
            $"LEGEND-RESOURCE-MANA-PAY-{profile.SourceObjectId}",
            "LEGEND_RESOURCE_PAYMENT",
            "P1",
            manaCost: 1,
            legalPaymentChoiceIds: ["SPEND_MANA:1"],
            reason: "LEGEND_GENERATED_MANA_DUPLICATE_PREVENTION");
        var paymentState = gained.State with
        {
            PendingPayment = pendingPayment
        };
        var paid = await PayCostAsync(paymentState, pendingPayment, ["SPEND_MANA:1"], profile, "mana-duplicate-first");
        Assert.True(paid.Accepted, paid.ErrorMessage);
        var afterSpendHash = MatchStateHasher.Hash(paid.State);

        var duplicate = await PayCostAsync(paid.State, pendingPayment, ["SPEND_MANA:1"], profile, "mana-duplicate-second");

        AssertRejectedNoMutation(duplicate, afterSpendHash);
        Assert.Equal(RunePool.Empty, duplicate.State.RunePools["P1"]);
    }

    [Theory]
    [MemberData(nameof(PowerSuccessProfiles))]
    public async Task LegendResourceBridgeGeneratedPowerCannotBeSpentTwice(BridgeProfile profile)
    {
        var gained = await ResolveLegendActAsync(BuildSuccessState(profile), profile);
        Assert.True(gained.Accepted, gained.ErrorMessage);
        var pendingPayment = new PendingPaymentState(
            $"LEGEND-RESOURCE-POWER-PAY-{profile.SourceObjectId}",
            "LEGEND_RESOURCE_PAYMENT",
            "P1",
            powerCost: 1,
            legalPaymentChoiceIds: ["SPEND_POWER:1"],
            reason: "LEGEND_GENERATED_POWER_DUPLICATE_PREVENTION");
        var paymentState = gained.State with
        {
            PendingPayment = pendingPayment
        };
        var paid = await PayCostAsync(paymentState, pendingPayment, ["SPEND_POWER:1"], profile, "power-duplicate-first");
        Assert.True(paid.Accepted, paid.ErrorMessage);
        var afterSpendHash = MatchStateHasher.Hash(paid.State);

        var duplicate = await PayCostAsync(paid.State, pendingPayment, ["SPEND_POWER:1"], profile, "power-duplicate-second");

        AssertRejectedNoMutation(duplicate, afterSpendHash);
        Assert.Equal(RunePool.Empty, duplicate.State.RunePools["P1"]);
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

    private static void AssertNoLegendActPromptForSource(MatchState state, BridgeProfile profile)
    {
        var p1Prompt = ResolutionResult.BuildPrompts(state)["P1"];

        Assert.DoesNotContain(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.LegendAct, StringComparison.Ordinal)
                && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, profile.SourceObjectId, StringComparison.Ordinal)));
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
        Assert.Equal(profile.CardNo, resourceEvent.Payload["cardNo"]);
        Assert.Contains(profile.CardNo, Assert.IsType<string[]>(resourceEvent.Payload["sourceCardNos"]));
        Assert.Equal(profile.AbilityId, resourceEvent.Payload["abilityId"]);
        Assert.Equal(BridgeGroupFor(profile), resourceEvent.Payload["bridgeGroup"]);
        Assert.Equal(profile.ResourceKind, resourceEvent.Payload["resourceKind"]);
        Assert.Equal("rune-pool-cleared-at-turn-end", resourceEvent.Payload["resourceLifecycle"]);
        Assert.True(Assert.IsType<bool>(resourceEvent.Payload["generatedResource"]));
        Assert.True(
            resourceEvent.Payload.TryGetValue("amount", out var amount),
            $"{eventKind} must expose a normalized amount payload field for the legend resource bridge.");
        Assert.Equal(1, amount);
    }

    private static string ResourceEventKind(BridgeProfile profile)
    {
        return string.Equals(profile.ResourceKind, "mana", StringComparison.Ordinal)
            ? "MANA_GAINED"
            : "POWER_GAINED";
    }

    private static int CountEvents(IReadOnlyList<GameEvent> events, string kind)
    {
        return events.Count(gameEvent => string.Equals(gameEvent.Kind, kind, StringComparison.Ordinal));
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

    private static async Task<ResolutionResult> PayCostAsync(
        MatchState state,
        PendingPaymentState pendingPayment,
        IReadOnlyList<string> paymentChoiceIds,
        BridgeProfile profile,
        string intentSuffix)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-legend-resource-bridge-{intentSuffix}-{profile.Name}-{profile.CardNo}", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, paymentChoiceIds),
            CancellationToken.None);
    }

    private static void AssertRejectedNoMutation(ResolutionResult result, string initialStateHash)
    {
        Assert.False(result.Accepted);
        Assert.Equal(initialStateHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    private static MatchState ExhaustSource(MatchState state, string sourceObjectId)
    {
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[sourceObjectId] = cardObjects[sourceObjectId] with
        {
            IsExhausted = true
        };
        return state with
        {
            CardObjects = cardObjects
        };
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

    private static MatchState MoveToOpenMainForEndTurn(MatchState state)
    {
        return state with
        {
            ActivePlayerId = "P1",
            TurnPlayerId = "P1",
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            PriorityPlayerId = null,
            PassedPriorityPlayerIds = [],
            FocusPlayerId = null,
            PassedFocusPlayerIds = [],
            StackItems = []
        };
    }

    private static string BridgeGroupFor(BridgeProfile profile)
    {
        return profile.AbilityId switch
        {
            DianaAbilityId => "diana-spell-duel-mana",
            DariusAbilityId => "darius-inspire-mana",
            OrnnAbilityId => "ornn-equipment-power",
            KaisaAbilityId => "kaisa-spell-power",
            _ => throw new InvalidOperationException($"Unknown bridge ability: {profile.AbilityId}")
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
