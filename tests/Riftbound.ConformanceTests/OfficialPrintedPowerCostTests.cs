using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialPrintedPowerCostTests
{
    [Fact]
    public void RainbowPowerPaysColoredCostWithoutSpendingOtherColors()
    {
        var pool = new RunePool(0, 1, new Dictionary<string, int> { ["blue"] = 2 });
        var cost = new Dictionary<string, int> { ["red"] = 1 };
        Assert.True(PaymentCostRules.CanPayPowerCost(pool, 0, cost));
        var remaining = PaymentCostRules.PayPowerCost(pool, 0, cost);
        Assert.Equal(0, remaining.AnyPower);
        Assert.Equal(2, remaining.PowerByTrait["blue"]);
        Assert.False(PaymentCostRules.CanPayPowerCost(pool, 1, new Dictionary<string, int> { ["red"] = 2 }));
    }

    [Theory]
    [InlineData(0, 0, 0, false)]
    [InlineData(0, 0, 1, false)]
    [InlineData(0, 1, 0, true)]
    [InlineData(1, 0, 0, true)]
    public async Task OrdinaryUnitPaysPrintedPower(int rainbow, int red, int blue, bool accepted)
    {
        var state = Position("SFD·006/221", new(3, rainbow, new Dictionary<string, int> { ["red"] = red, ["blue"] = blue }));
        var before = MatchStateHasher.Hash(state);
        var result = await Play(state);
        Assert.Equal(accepted, result.Accepted);
        if (accepted)
        {
            Assert.Equal(0, result.State.RunePools["P1"].TotalPower);
            Assert.Equal(0, result.State.RunePools["P1"].Mana);
        }
        else
        {
            Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
            Assert.Equal(before, MatchStateHasher.Hash(result.State));
        }
    }

    [Theory]
    [InlineData(2, 0, 0, true)]
    [InlineData(0, 2, 0, true)]
    [InlineData(1, 1, 0, true)]
    [InlineData(0, 0, 2, true)]
    [InlineData(1, 0, 1, true)]
    [InlineData(1, 0, 0, false)]
    public async Task MulticolorPrintedCostIsSharedAcrossItsTraits(int green, int blue, int rainbow, bool accepted)
    {
        var result = await Play(Position("SFD·190/221", new(4, rainbow,
            new Dictionary<string, int> { ["green"] = green, ["blue"] = blue })));
        Assert.Equal(accepted, result.Accepted);
        if (accepted) Assert.Equal(0, result.State.RunePools["P1"].TotalPower);
    }

    [Fact]
    public async Task PlayerChoosesWhichPrintedTraitToSpend()
    {
        var state = Position("SFD·190/221", new(4, 0, new Dictionary<string, int> { ["green"] = 2, ["blue"] = 2 }));
        var result = await Play(state, ["PRINTED_POWER:green:2"]);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(2, result.State.RunePools["P1"].PowerByTrait["blue"]);
        Assert.False(result.State.RunePools["P1"].PowerByTrait.ContainsKey("green"));
    }

    [Theory]
    [InlineData("PRINTED_POWER:red:2")]
    [InlineData("PRINTED_POWER:green:1")]
    [InlineData("PRINTED_POWER:green:-2")]
    public async Task ForgedPrintedPaymentCannotChangeCost(string choice)
    {
        var state = Position("SFD·190/221", new(4, 20));
        var before = MatchStateHasher.Hash(state);
        var result = await Play(state, [choice]);
        Assert.False(result.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    private static MatchState Position(string cardNo, RunePool pool)
        => new("PRINTED-COST", 1, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = pool, ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["CARD"] }, ["P2"] = PlayerZones.Empty },
            cardObjects: new Dictionary<string, CardObjectState> { ["CARD"] = new("CARD", cardNo: cardNo, ownerId: "P1", controllerId: "P1") });

    // CN core 164.2.b, 166-167 and 430.2-430.4: gaining resources is a
    // player choice; the cost consumes only the required amount from the pool.
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task PlayerMayRecycleMoreRunesThanPrintedCostNeeds(int existingRedPower, bool exhausted)
    {
        var state = SurplusRunePosition(existingRedPower, exhausted);
        var before = MatchStateHasher.Hash(state);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]));
        var resources = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]);
        Assert.Equal(["RECYCLE_RUNE:RUNE-A", "RECYCLE_RUNE:RUNE-B"], resources.Select(c => c.Id));
        var command = new PlayCardCommand("CARD", "SFD·006/221", [],
            OptionalCosts: ["RECYCLE_RUNE:RUNE-B", "RECYCLE_RUNE:RUNE-A"]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", new("surplus-quote", "prompt", state.Tick, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(before, MatchStateHasher.Hash(state));

        var result = await engine.ResolveAsync(state, new("surplus-play", "P1", CommandTypes.PlayCard), command, default);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(existingRedPower + 1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(["BOTTOM", "RUNE-B", "RUNE-A"], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(["CARD"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["RUNE-B", "RUNE-A"], result.Events.Where(e => e.Kind == "RUNE_RECYCLED")
            .Select(e => Assert.IsType<string>(e.Payload["sourceObjectId"])));
        var paid = Assert.Single(result.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(["RECYCLE_RUNE:RUNE-B", "RECYCLE_RUNE:RUNE-A"], Assert.IsType<string[]>(paid.Payload["paymentResourceActions"]));
        Assert.Equal(1, paid.Payload["totalPowerCost"]);
        Assert.Equal(quote.Cost!.RemainingPowerByTrait![RuneTrait.Red], result.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("hidden")]
    [InlineData("not-rune")]
    [InlineData("missing")]
    [InlineData("wrong-trait")]
    public async Task ExtraRuneAcquisitionStillRejectsInvalidResourcesOrUnpaidCost(string scenario)
    {
        var state = SurplusRunePosition(scenario == "wrong-trait" ? 0 : 1, true);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        cards["RUNE-A"] = scenario switch
        {
            "foreign" => cards["RUNE-A"] with { ControllerId = "P2", OwnerId = "P2" },
            "hidden" => cards["RUNE-A"] with { IsFaceDown = true },
            "not-rune" => cards["RUNE-A"] with { Tags = [CardObjectTags.UnitCard] },
            "wrong-trait" => cards["RUNE-A"] with { Tags = [CardObjectTags.RuneCard, "COLOR:blue"] },
            _ => cards["RUNE-A"]
        };
        state = state with { CardObjects = cards };
        var before = MatchStateHasher.Hash(state);
        var result = await Play(state, [$"RECYCLE_RUNE:{(scenario == "missing" ? "MISSING" : "RUNE-A")}"]);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task ServerDoesNotChooseRuneAcquisitionForPlayer(int existingRedPower, bool accepted)
    {
        var state = SurplusRunePosition(existingRedPower, false);
        var result = await Play(state);
        Assert.Equal(accepted, result.Accepted);
        Assert.Equal(["BOTTOM"], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Contains("RUNE-A", result.State.PlayerZones["P1"].Base);
        Assert.Contains("RUNE-B", result.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(result.Events, e => e.Kind is "RUNE_RECYCLED" or "POWER_GAINED");
        if (!accepted) Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    private static MatchState SurplusRunePosition(int redPower, bool exhausted)
    {
        var state = Position("SFD·006/221", new(3, 0, new Dictionary<string, int> { [RuneTrait.Red] = redPower }));
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        foreach (var id in new[] { "RUNE-A", "RUNE-B", "BOTTOM" })
            cards[id] = new(id, cardNo: "UNL-R01", isExhausted: exhausted,
                tags: [CardObjectTags.RuneCard, "COLOR:red"], ownerId: "P1", controllerId: "P1");
        return state with { CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones> {
                ["P1"] = state.PlayerZones["P1"] with { Base = ["RUNE-A", "RUNE-B"], RuneDeck = ["BOTTOM"] },
                ["P2"] = PlayerZones.Empty
            } };
    }

    [Fact]
    public async Task TwoRunePaymentIsOfferedAndCommittedAtomically()
    {
        var state = Position("SFD·190/221", new(4, 0));
        var cards = state.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        foreach (var trait in new[] { "green", "blue", "red" })
            cards[trait] = new(trait, cardNo: "TEST-RUNE-" + trait, tags: [CardObjectTags.RuneCard, $"COLOR:{trait}"], ownerId: "P1", controllerId: "P1");
        state = state with { CardObjects = cards, PlayerZones = new Dictionary<string, PlayerZones>
        {
            ["P1"] = state.PlayerZones["P1"] with { Base = ["green", "blue", "red"] }, ["P2"] = PlayerZones.Empty
        } };
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, x => x.Action == CommandTypes.PlayCard);
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]));
        var resources = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]);
        Assert.Equal(["RECYCLE_RUNE:blue", "RECYCLE_RUNE:green"], resources.Select(x => x.Id));
        Assert.Equal(2, requirement["printedPowerCost"]);
        Assert.Equal(3, Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["printedPowerChoices"]).Count);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_NATIVE_PAYMENT_FIXTURE") is { Length: > 0 } path)
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(candidate, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        var rejected = await Play(state, ["RECYCLE_RUNE:green"]);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
        var accepted = await Play(state, ["RECYCLE_RUNE:blue", "RECYCLE_RUNE:green"]);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(RunePool.Empty, accepted.State.RunePools["P1"]);
        Assert.Equal(["red", "CARD"], accepted.State.PlayerZones["P1"].Base);
        Assert.Equal(2, accepted.State.PlayerZones["P1"].RuneDeck.Count);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_NATIVE_PAYMENT_COMMAND") is { Length: > 0 } commandPath)
        {
            using var json = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(commandPath));
            var command = Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(json.RootElement));
            var native = await new CoreRuleEngine().ResolveAsync(state, new("native-payment", "P1", CommandTypes.PlayCard), command, default);
            Assert.True(native.Accepted, native.ErrorMessage);
            Assert.Equal(MatchStateHasher.Hash(accepted.State), MatchStateHasher.Hash(native.State));
        }
    }

    [Fact]
    public async Task PrintedCostAndHasteArePaidSeparately()
    {
        var insufficient = Position("SFD·002/221", new(7, 1));
        var rejected = await Play(insufficient, [HasteOptionalCostNames.HasteReady]);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(insufficient), MatchStateHasher.Hash(rejected.State));
        var accepted = await Play(Position("SFD·002/221", new(7, 2)), [HasteOptionalCostNames.HasteReady]);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(RunePool.Empty, accepted.State.RunePools["P1"]);
    }

    private static ValueTask<ResolutionResult> Play(MatchState state, string[]? costs = null)
        => new CoreRuleEngine().ResolveAsync(state, new("play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("CARD", state.CardObjects["CARD"].CardNo!, [], OptionalCosts: costs), default);
}
