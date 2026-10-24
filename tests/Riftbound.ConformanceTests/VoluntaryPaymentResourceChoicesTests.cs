using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class VoluntaryPaymentResourceChoicesTests
{
    // CN 164.2.b, 166-167, 204.4.b.1 and 430: the acquired resource need not
    // contribute to this particular fee; the fee still requires its exact trait.
    [Theory]
    [InlineData(RuneTrait.Red)]
    [InlineData(RuneTrait.Green)]
    [InlineData(RuneTrait.Blue)]
    [InlineData(RuneTrait.Orange)]
    [InlineData(RuneTrait.Purple)]
    [InlineData(RuneTrait.Yellow)]
    public async Task MightyYellowPaymentPermitsVoluntaryAcquisitionOfEveryRuneTrait(string trait)
    {
        var initial = WithRune(TriggerPaymentTests.BuildFioraPowerfulReadyState(yellowPower: 1), trait);
        var engine = new CoreRuleEngine();
        var opened = await TriggerPaymentTests.ResolveFioraPowerfulReadyTriggerAsync(initial, engine);
        var payment = Assert.IsType<PendingPaymentState>(opened.State.PendingPayment);
        var before = MatchStateHasher.Hash(opened.State);
        Assert.Equal(1, payment.PowerCostByTrait[RuneTrait.Yellow]);
        Assert.Contains("RECYCLE_RUNE:EXTRA-RUNE", payment.PaymentResourceActionIds);

        var paid = await engine.ResolveAsync(opened.State, new("voluntary-any-trait", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow,
                ["RECYCLE_RUNE:EXTRA-RUNE", "SPEND_POWER:yellow:1"]), default);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Equal(before, MatchStateHasher.Hash(opened.State));
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(1, paid.State.RunePools["P1"].PowerByTrait[trait]);
        Assert.Equal(1, paid.State.RunePools["P1"].TotalPower);
        Assert.Contains("EXTRA-RUNE", paid.State.PlayerZones["P1"].RuneDeck);
        Assert.True(paid.State.CardObjects["P1-FIORA-TARGET"].IsExhausted);
        var cost = Assert.Single(paid.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(["RECYCLE_RUNE:EXTRA-RUNE"], Assert.IsType<string[]>(cost.Payload["paymentResourceActions"]));
        Assert.Equal(payment.PaymentId, cost.Payload["paymentId"]);
        var resolved = await OfficialGraveyardRecastTests.Top(paid.State);
        Assert.False(resolved.State.CardObjects["P1-FIORA-TARGET"].IsExhausted);
    }

    [Theory]
    [InlineData(RuneTrait.Red)]
    [InlineData(RuneTrait.Green)]
    [InlineData(RuneTrait.Blue)]
    [InlineData(RuneTrait.Orange)]
    [InlineData(RuneTrait.Purple)]
    [InlineData(RuneTrait.Yellow)]
    public async Task SeparateRuneAcquisitionKeepsTheSameMightyFeeAndRefreshesItsRemainingResourceChoices(string trait)
    {
        var engine = new CoreRuleEngine();
        var opened = await TriggerPaymentTests.ResolveFioraPowerfulReadyTriggerAsync(
            WithRune(TriggerPaymentTests.BuildFioraPowerfulReadyState(yellowPower: 1), trait), engine);
        var original = opened.State.PendingPayment!;
        var acquired = await engine.ResolveAsync(opened.State, new("separate-voluntary-rune", "P1", CommandTypes.RecycleRune),
            new RecycleRuneCommand("EXTRA-RUNE"), default);
        Assert.True(acquired.Accepted, acquired.ErrorMessage);
        Assert.Equal(opened.State.PriorityPlayerId, acquired.State.PriorityPlayerId);
        Assert.Equal(opened.State.ActivePlayerId, acquired.State.ActivePlayerId);
        Assert.Equal(opened.State.TimingState, acquired.State.TimingState);
        var remaining = acquired.State.PendingPayment!;
        Assert.Equal(original.PaymentId, remaining.PaymentId);
        Assert.Equal(original.PaymentWindow, remaining.PaymentWindow);
        Assert.Equal(1, remaining.PowerCostByTrait[RuneTrait.Yellow]);
        Assert.Empty(remaining.PaymentResourceActionIds);
        var paid = await engine.ResolveAsync(acquired.State, new("pay-after-voluntary-rune", "P1", CommandTypes.PayCost),
            new PayCostCommand(remaining.PaymentId, remaining.PaymentWindow, ["SPEND_POWER:yellow:1"]), default);
        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Equal(1, paid.State.RunePools["P1"].PowerByTrait[trait]);
        Assert.Equal(1, paid.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(paid.Events, e => e.Kind == "RUNE_RECYCLED");
    }

    [Theory]
    [InlineData(RuneTrait.Red)]
    [InlineData(RuneTrait.Green)]
    [InlineData(RuneTrait.Blue)]
    [InlineData(RuneTrait.Orange)]
    [InlineData(RuneTrait.Purple)]
    public async Task UnrelatedAcquiredPowerCannotReplaceTheRequiredYellowFee(string trait)
    {
        var engine = new CoreRuleEngine();
        var opened = await TriggerPaymentTests.ResolveFioraPowerfulReadyTriggerAsync(
            WithRune(TriggerPaymentTests.BuildFioraPowerfulReadyState(), trait), engine);
        var payment = opened.State.PendingPayment!;
        Assert.Contains("RECYCLE_RUNE:EXTRA-RUNE", payment.PaymentResourceActionIds);
        var before = MatchStateHasher.Hash(opened.State);
        var rejected = await engine.ResolveAsync(opened.State, new("wrong-trait-fee", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["RECYCLE_RUNE:EXTRA-RUNE", "SPEND_POWER:yellow:1"]), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("hidden")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public async Task VoluntaryMightyAcquisitionStillRejectsInvalidResourceIntentWithoutMutation(string scenario)
    {
        var initial = WithRune(TriggerPaymentTests.BuildFioraPowerfulReadyState(yellowPower: 1), RuneTrait.Blue);
        var cards = initial.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        cards["EXTRA-RUNE"] = scenario switch {
            "foreign" => cards["EXTRA-RUNE"] with { ControllerId = "P2", OwnerId = "P2" },
            "hidden" => cards["EXTRA-RUNE"] with { IsFaceDown = true },
            _ => cards["EXTRA-RUNE"]
        };
        initial = initial with { CardObjects = cards };
        var engine = new CoreRuleEngine();
        var opened = await TriggerPaymentTests.ResolveFioraPowerfulReadyTriggerAsync(initial, engine);
        var payment = opened.State.PendingPayment!;
        var before = MatchStateHasher.Hash(opened.State);
        string[] choices = scenario switch {
            "missing" => ["RECYCLE_RUNE:MISSING", "SPEND_POWER:yellow:1"],
            "duplicate" => ["RECYCLE_RUNE:EXTRA-RUNE", "RECYCLE_RUNE:EXTRA-RUNE", "SPEND_POWER:yellow:1"],
            _ => ["RECYCLE_RUNE:EXTRA-RUNE", "SPEND_POWER:yellow:1"]
        };
        var rejected = await engine.ResolveAsync(opened.State, new("invalid-voluntary-" + scenario, "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, choices), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task PrintedRedCostQuotesAndAllowsAnUnrelatedBlueResourceWithoutUsingItForTheRedFee(int redPower)
    {
        var initial = new MatchState("voluntary-printed-resource", 0, 1, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, status: MatchStatuses.InProgress,
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(3, 0, new Dictionary<string, int> { [RuneTrait.Red] = redPower }), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["CARD"] }, ["P2"] = PlayerZones.Empty },
            cardObjects: new Dictionary<string, CardObjectState> { ["CARD"] = new("CARD", cardNo: "SFD·006/221", ownerId: "P1", controllerId: "P1") });
        initial = WithRune(WithRune(initial, RuneTrait.Red, "RED-RUNE"), RuneTrait.Blue);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirement = Requirement(candidate);
        Assert.Contains(Resources(requirement), c => c.Id == "RECYCLE_RUNE:EXTRA-RUNE");
        string[] choices = redPower == 0 ? ["RECYCLE_RUNE:RED-RUNE", "RECYCLE_RUNE:EXTRA-RUNE"] : ["RECYCLE_RUNE:EXTRA-RUNE"];
        var command = new PlayCardCommand("CARD", "SFD·006/221", [], OptionalCosts: choices);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        var result = await engine.ResolveAsync(initial, new("voluntary-blue-play", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        Assert.Equal(1, result.State.RunePools["P1"].TotalPower);
        Assert.Equal(redPower == 1, result.State.PlayerZones["P1"].Base.Contains("RED-RUNE"));
    }

    [Fact]
    public async Task TypedAssembleFeeQuotesAndAllowsVoluntaryBlueAcquisition()
    {
        var initial = WithRune(PaymentEngineUnificationTests.AssembleState(
            new(0, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 })), RuneTrait.Blue);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.AssembleEquipment);
        Assert.Contains(Resources(Requirement(candidate)), c => c.Id == "RECYCLE_RUNE:EXTRA-RUNE");
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("voluntary-blue-assemble", "P1", CommandTypes.AssembleEquipment),
            new AssembleEquipmentCommand("P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET", ["ASSEMBLE_RED", "RECYCLE_RUNE:EXTRA-RUNE"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        Assert.Equal(1, result.State.RunePools["P1"].TotalPower);
    }

    [Fact]
    public async Task ManaOnlyPlayQuotesAnUnrelatedPowerAcquisitionWithoutAddingAPowerFee()
    {
        var initial = new MatchState("voluntary-mana-only", 0, 1, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, status: MatchStatuses.InProgress,
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(2, 0), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["CARD"] }, ["P2"] = PlayerZones.Empty },
            cardObjects: new Dictionary<string, CardObjectState> { ["CARD"] = new("CARD", cardNo: "SFD·065/221", ownerId: "P1", controllerId: "P1") });
        initial = WithRune(initial, RuneTrait.Blue);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(Resources(Requirement(candidate)), c => c.Id == "RECYCLE_RUNE:EXTRA-RUNE");
        var command = new PlayCardCommand("CARD", "SFD·065/221", [], OptionalCosts: ["RECYCLE_RUNE:EXTRA-RUNE"]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.GenericPower);
        Assert.Empty(quote.Cost.PowerByTrait);
        var played = await engine.ResolveAsync(initial, new("voluntary-mana-only", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Equal(1, played.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
    }

    [Fact]
    public async Task TypedActivatedAbilityQuotesAndAllowsUnrelatedRedAcquisition()
    {
        var initial = WithRune(RenataActivatedAbilityTests.AffordableBlueDrawPaymentState(), RuneTrait.Red);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        Assert.Contains(Resources(Requirement(candidate)), c => c.Id == "RECYCLE_RUNE:EXTRA-RUNE");
        var source = initial.CardObjects.Values.Single(card => card.CardNo == P4ActivatedAbilityCatalog.RenataGlascCardNo);
        var activated = await new CoreRuleEngine().ResolveAsync(initial, new("voluntary-red-activate", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand(source.ObjectId, P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId, [], ["RECYCLE_RUNE:EXTRA-RUNE"]), default);
        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.Equal(0, activated.State.RunePools["P1"].Mana);
        Assert.Equal(1, activated.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, activated.State.RunePools["P1"].TotalPower);
    }

    private static IReadOnlyDictionary<string, object?> Requirement(ActionPromptCandidateDto candidate)
        => Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]));

    private static IEnumerable<ActionPromptChoiceDto> Resources(IReadOnlyDictionary<string, object?> requirement)
        => Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]);

    internal static MatchState WithRune(MatchState state, string trait, string id = "EXTRA-RUNE")
    {
        var number = trait switch { RuneTrait.Red => "OGN·007/298", RuneTrait.Green => "OGN·042/298",
            RuneTrait.Blue => "OGN·089/298", RuneTrait.Orange => "OGN·126/298", RuneTrait.Purple => "OGN·166/298",
            RuneTrait.Yellow => "OGN·214/298", _ => throw new ArgumentOutOfRangeException(nameof(trait)) };
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = new(id, cardNo: number, isExhausted: true, tags: [CardObjectTags.RuneCard, "COLOR:" + trait], ownerId: "P1", controllerId: "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = state.PlayerZones["P1"].Base.Append(id).ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", "BASE") }
        };
    }
}
