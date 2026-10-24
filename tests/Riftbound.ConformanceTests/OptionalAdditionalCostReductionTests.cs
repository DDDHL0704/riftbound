using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN 356.4.c/f.1 and current SFD official FAQ p20: choose a reduction for
// the optional additional cost; an unchosen reduction never changes its price.
public sealed class OptionalAdditionalCostReductionTests
{
    private const string ManaReduction = "OPTIONAL_COST_REDUCTION:EZREAL:0:ECHO:MANA";

    [Theory]
    [InlineData("SFD·149/221")]
    [InlineData("SFD·149a/221")]
    public async Task ExplicitManaReductionMakesEchoAffordableAndKeepsTheDeclaredRepeat(string reducer)
    {
        var initial = Position(reducer, mana: 3);
        var before = MatchStateHasher.Hash(initial);
        var engine = new CoreRuleEngine();
        var command = Command("ECHO", ManaReduction);

        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.RemainingMana);
        Assert.Equal(before, MatchStateHasher.Hash(initial));

        var played = await engine.ResolveAsync(initial,
            new("explicit-echo-reduction", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(before, MatchStateHasher.Hash(initial));
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Equal(2, Assert.Single(played.State.StackItems).EffectRepeatCount);
        var paid = Assert.Single(played.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(["ECHO"], Assert.IsType<string[]>(paid.Payload["optionalCosts"]));
        Assert.Equal([ManaReduction], Assert.IsType<string[]>(paid.Payload["costReductionChoices"]));
    }

    [Theory]
    [InlineData("SFD·149/221", 3)]
    [InlineData("SFD·149/221", 4)]
    [InlineData("SFD·149a/221", 3)]
    [InlineData("SFD·149a/221", 4)]
    public async Task OmittingTheReductionChoiceKeepsTheFullEchoFee(string reducer, int mana)
    {
        var initial = Position(reducer, mana);
        var before = MatchStateHasher.Hash(initial);
        var engine = new CoreRuleEngine();
        var command = Command("ECHO");
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.IsValid, quote.Message);
        Assert.Equal(4, quote.Cost!.Mana);
        Assert.Equal(mana >= 4, quote.CanPay);

        var played = await engine.ResolveAsync(initial,
            new("no-implicit-echo-reduction", "P1", CommandTypes.PlayCard), command, default);
        Assert.Equal(mana >= 4, played.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(initial));
        if (mana < 4)
        {
            Assert.Equal(ErrorCodes.InsufficientCost, played.ErrorCode);
            Assert.Empty(played.Events);
            Assert.Equal(before, MatchStateHasher.Hash(played.State));
        }
        else
        {
            Assert.Equal(0, played.State.RunePools["P1"].Mana);
            Assert.Equal(2, Assert.Single(played.State.StackItems).EffectRepeatCount);
            var paid = Assert.Single(played.Events, e => e.Kind == "COST_PAID");
            Assert.Equal(["ECHO"], Assert.IsType<string[]>(paid.Payload["optionalCosts"]));
            if (paid.Payload.TryGetValue("costReductionChoices", out var choices))
                Assert.Empty(Assert.IsAssignableFrom<IEnumerable<string>>(choices));
        }
    }

    [Theory]
    [InlineData("SFD·149/221")]
    [InlineData("SFD·149a/221")]
    public void PromptOffersTheExplicitReductionRouteForAnOtherwiseUnaffordableEcho(string reducer)
    {
        var initial = Position(reducer, mana: 3);
        var play = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!,
            c => c.Action == CommandTypes.PlayCard);
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            play.Metadata!["sourceRequirements"]));
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]);
        Assert.Contains(choices, c => c.Id == "ECHO");
        Assert.Contains(choices, c => c.Id == ManaReduction);
    }

    [Theory]
    [InlineData("SFD·149/221")]
    [InlineData("SFD·149a/221")]
    public async Task GenericEchoPowerReductionPreservesTheSpellsPrintedPowerFee(string reducer)
    {
        const string reduction = "OPTIONAL_COST_REDUCTION:EZREAL:0:ECHO:POWER-any";
        var initial = Position(reducer, 2, "SFD·182/221", new(2, 0,
            new Dictionary<string, int> { [RuneTrait.Red] = 1 }));
        var command = new PlayCardCommand("CARD", "SFD·182/221", [],
            OptionalCosts: ["ECHO", reduction, "PRINTED_POWER:red:1"]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Red]);
        var played = await engine.ResolveAsync(initial, new("reduce-echo-not-printed", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, Assert.Single(played.State.StackItems).EffectRepeatCount);
        Assert.Equal(1, Assert.Single(played.Events, e => e.Kind == "COST_PAID").Payload["power"]);
    }

    [Theory]
    [InlineData("SFD·149/221")]
    [InlineData("SFD·149a/221")]
    public async Task TypedHasteReductionToZeroStillPaysTheDeclaredReadyEffect(string reducer)
    {
        const string reduction = "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:POWER-red";
        var initial = Position(reducer, 4, "SFD·029/221");
        var command = new PlayCardCommand("CARD", "SFD·029/221", [],
            OptionalCosts: [HasteOptionalCostNames.HasteReady, reduction]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(4, quote.Cost!.Mana);
        Assert.Empty(quote.Cost.PowerByTrait);
        var played = await engine.ResolveAsync(initial, new("free-typed-haste", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        var paid = Assert.Single(played.Events, e => e.Kind == "COST_PAID");
        Assert.Equal([HasteOptionalCostNames.HasteReady], Assert.IsType<string[]>(paid.Payload["optionalCosts"]));
        Assert.Equal(0, paid.Payload["power"]);
        var resolved = await PermanentConfirmationAssert.ResolveAfterPlayAsync(engine, played);
        Assert.False(resolved.State.CardObjects["CARD"].IsExhausted);
    }

    [Fact]
    public async Task TwoSelectedReductionsPayAZeroManaEchoAndKeepItsRepeat()
    {
        var initial = WithSecondReducer(Position("SFD·149/221", 2));
        var command = Command("ECHO", ManaReduction, "OPTIONAL_COST_REDUCTION:EZREAL-TWO:0:ECHO:MANA");
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana);
        var played = await engine.ResolveAsync(initial, new("free-echo-still-repeats", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(2, Assert.Single(played.State.StackItems).EffectRepeatCount);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
    }

    [Fact]
    public async Task ExcessComponentReductionsNeverSpillIntoBaseMana()
    {
        var initial = WithSecondReducer(Position("SFD·149/221", 3, "SFD·029/221",
            new(3, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 })));
        var command = new PlayCardCommand("CARD", "SFD·029/221", [], OptionalCosts: [HasteOptionalCostNames.HasteReady,
            "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:MANA",
            "OPTIONAL_COST_REDUCTION:EZREAL-TWO:0:ADDITIONAL:MANA"]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Red]);
        var played = await engine.ResolveAsync(initial, new("no-component-spill", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Equal(1, Assert.Single(played.Events, e => e.Kind == "COST_PAID").Payload["optionalAdditionalManaReduction"]);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("hidden")]
    [InlineData("graveyard")]
    [InlineData("stale-generation")]
    [InlineData("missing-location")]
    [InlineData("no-capability")]
    [InlineData("duplicate")]
    [InlineData("fee-not-declared")]
    [InlineData("wrong-component")]
    public async Task InvalidReductionChoicesRejectWithoutPayingAnyPart(string scenario)
    {
        var initial = Position("SFD·149/221", 3);
        if (scenario is "hidden" or "stale-generation" or "no-capability")
        {
            var source = initial.CardObjects["EZREAL"];
            source = scenario switch {
                "hidden" => source with { IsFaceDown = true },
                "stale-generation" => source with { ObjectGeneration = 1 },
                _ => OfficialGarenConquestBattlefieldCountTests.Printed("EZREAL", "OGN·003/298", "P1") };
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["EZREAL"] = source } };
        }
        if (scenario == "missing-location")
            initial = initial with { ObjectLocations = initial.ObjectLocations.Where(p => p.Key != "EZREAL").ToDictionary(p => p.Key, p => p.Value) };
        if (scenario is "foreign" or "graveyard")
        {
            var zones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Value);
            zones["P1"] = zones["P1"] with { Base = [] };
            if (scenario == "foreign") zones["P2"] = zones["P2"] with { Base = zones["P2"].Base.Append("EZREAL").ToArray() };
            else zones["P1"] = zones["P1"] with { Graveyard = ["EZREAL"] };
            initial = initial with { PlayerZones = zones,
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    ["EZREAL"] = initial.CardObjects["EZREAL"] with {
                        ControllerId = scenario == "foreign" ? "P2" : "P1", OwnerId = scenario == "foreign" ? "P2" : "P1" } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                    ["EZREAL"] = new(scenario == "foreign" ? "P2" : "P1", scenario == "foreign" ? "BASE" : "GRAVEYARD") } };
        }
        var costs = scenario switch {
            "duplicate" => new[] { "ECHO", ManaReduction, ManaReduction },
            "fee-not-declared" => [ManaReduction],
            "wrong-component" => ["ECHO", "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:MANA"],
            _ => ["ECHO", ManaReduction] };
        var before = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial,
            new("invalid-reducer-" + scenario, "P1", CommandTypes.PlayCard), Command(costs), default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(before, MatchStateHasher.Hash(initial));
    }

    [Fact]
    public async Task OneSourceCannotChooseBothManaAndPowerForTheSameHasteFee()
    {
        var initial = Position("SFD·149/221", 4, "SFD·029/221",
            new(4, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }));
        var before = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("one-source-one-mode", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("CARD", "SFD·029/221", [], OptionalCosts: [HasteOptionalCostNames.HasteReady,
                "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:MANA", "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:POWER-red"]), default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("SFD·149/221")]
    [InlineData("SFD·149a/221")]
    public void PromptOffersTheExplicitBlastCrewManaReduction(string reducer)
    {
        var initial = WithBattlefieldTarget(Position(reducer, 2, "SFD·013/221",
            new(2, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 })));
        var play = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(play.Metadata!["sourceRequirements"]));
        var costs = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]);
        Assert.Contains(costs, c => c.Id == "SPEND_MANA:1");
        Assert.Contains(costs, c => c.Id == "SPEND_POWER:red:1");
        Assert.Contains(costs, c => c.Id == "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:MANA");
        var group = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(requirement["optionalCostReductionGroups"]));
        Assert.Equal("EZREAL", group["sourceObjectId"]);
        Assert.Equal(0L, group["sourceGeneration"]);
        Assert.Equal("ADDITIONAL", group["componentId"]);
        Assert.Equal(0, group["minSelections"]);
        Assert.Equal(1, group["maxSelections"]);
        Assert.Equal(["SPEND_MANA:1", "SPEND_POWER:red:1"],
            Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyList<string>>>(group["requiresOneOfOptionalCostSelections"])));
    }

    [Fact]
    public async Task AReductionDoesNotMakeABaseUnitALegalBlastCrewTarget()
    {
        var initial = Position("SFD·149/221", 2, "SFD·013/221",
            new(2, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }));
        var command = new PlayCardCommand("CARD", "SFD·013/221", [], OptionalCosts: ["SPEND_MANA:1", "SPEND_POWER:red:1",
            "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:MANA"]);
        var result = await new CoreRuleEngine().ResolveAsync(initial,
            new("paid-entry-still-no-battlefield-target", "P1", command.CmdType), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.State.PendingCardChoice);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(0, result.State.CardObjects["TARGET"].Damage);
        Assert.Equal(initial.CardObjects["TARGET"].Power, result.State.CardObjects["TARGET"].Power);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
    }

    [Theory]
    [InlineData("SFD·149/221")]
    [InlineData("SFD·149a/221")]
    public async Task CurrentSessionJournalsTheExplicitChoiceAndOriginalReplayMatches(string reducer)
    {
        var initial = Position(reducer, 3);
        var engine = new CoreRuleEngine();
        var journal = new ReductionJournal();
        var session = new MatchSession(initial, engine, journal);
        var command = Command("ECHO", ManaReduction);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(command, command.GetType(), json))!;
        fields["promptId"] = JsonSerializer.SerializeToElement(session.PromptFor("P1").PromptId);
        fields["snapshotTick"] = JsonSerializer.SerializeToElement(initial.Tick);
        var raw = JsonSerializer.SerializeToElement(fields);
        var paid = await session.SubmitAsync("P1", "journal-reduction", command, raw, default);
        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        var entries = journal.Entries.Count;
        var duplicate = await session.SubmitAsync("P1", "journal-reduction", command, raw, default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Equal(entries, journal.Entries.Count);
        Assert.Equal(MatchStateHasher.Hash(paid.State), MatchStateHasher.Hash(duplicate.State));
        var stale = await session.SubmitAsync("P1", "stale-reduction", command, raw, default);
        Assert.False(stale.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode);
        Assert.Empty(stale.Events);
        Assert.Equal(MatchStateHasher.Hash(paid.State), MatchStateHasher.Hash(stale.State));

        var commands = journal.Entries.Select(entry => new RecoveredCommand(entry.PlayerId, entry.ClientIntentId,
            entry.CommandType, entry.RawCommand, entry.StartedTick, entry.CompletedTick,
            entry.StartedEventSequence, entry.CompletedEventSequence, entry.Accepted, entry.ErrorMessage) { ErrorCode = entry.ErrorCode }).ToArray();
        var events = journal.Entries.SelectMany(entry => entry.Events.Select((ev, index) => new RecoveredEvent(
            entry.StartedEventSequence + index + 1, entry.CompletedTick, index, ev))).ToArray();
        var recordedCommand = Assert.IsType<JsonElement>(commands[0].RawCommand);
        Assert.Equal(["ECHO", ManaReduction], recordedCommand.GetProperty("optionalCosts")
            .EnumerateArray().Select(value => value.GetString()!).ToArray());
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, paid.State, engine, default, events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Theory]
    [InlineData("SFD·149/221")]
    [InlineData("SFD·149a/221")]
    public async Task GrantedEchoPowerChoiceBindsItsOwnPrintedAllocationAndPreservesBasePower(string reducer)
    {
        var initial = Position(reducer, 2, "SFD·182/221",
            new(2, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 })) with {
                UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var reduction = PrintedEchoReduction("EZREAL", RuneTrait.Blue);
        var command = new PlayCardCommand("CARD", "SFD·182/221", [],
            OptionalCosts: ["ECHO:GRANTED:1", "PRINTED_POWER:red:1", reduction]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower);
        Assert.Equal(1, Assert.Single(quote.Cost.PowerByTrait).Value);
        Assert.Equal(RuneTrait.Red, Assert.Single(quote.Cost.PowerByTrait).Key);
        var played = await engine.ResolveAsync(initial, new("printed-echo-allocation", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, Assert.Single(played.State.StackItems).EffectRepeatCount);
        Assert.Equal(1, Assert.Single(played.Events, e => e.Kind == "COST_PAID").Payload["optionalAdditionalPowerReduction"]);
    }

    [Fact]
    public async Task TwoSourcesCannotAssignDifferentOriginalPrintedAllocationsToOneEcho()
    {
        var initial = WithSecondReducer(Position("SFD·149/221", 10, "SFD·182/221", new(10, 10))) with {
            UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var before = MatchStateHasher.Hash(initial);
        var command = new PlayCardCommand("CARD", "SFD·182/221", [], OptionalCosts: ["ECHO:GRANTED:1",
            PrintedEchoReduction("EZREAL", RuneTrait.Red), PrintedEchoReduction("EZREAL-TWO", RuneTrait.Blue)]);
        var played = await new CoreRuleEngine().ResolveAsync(initial,
            new("inconsistent-echo-allocation", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(played.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, played.ErrorCode);
        Assert.Empty(played.Events);
        Assert.Equal(before, MatchStateHasher.Hash(played.State));
    }

    [Fact]
    public async Task OneSourceCanReduceEachOfTwoIndependentDeclaredEchoCosts()
    {
        var initial = Position("SFD·149/221", 4) with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var command = Command("ECHO", "ECHO:GRANTED:1", ManaReduction,
            "OPTIONAL_COST_REDUCTION:EZREAL:0:ECHO%3AGRANTED%3A1:MANA");
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(4, quote.Cost!.Mana);
        var played = await engine.ResolveAsync(initial, new("independent-echo-discounts", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(3, Assert.Single(played.State.StackItems).EffectRepeatCount);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Equal(2, Assert.Single(played.Events, e => e.Kind == "COST_PAID").Payload["optionalAdditionalManaReduction"]);
    }

    [Fact]
    public async Task MaraiAndExplicitReductionsClampWithinEchoBeforeTheTotalIsComputed()
    {
        var initial = WithSecondReducer(Position("SFD·149/221", 2));
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["MARAI"] = OfficialGarenConquestBattlefieldCountTests.Printed("MARAI", "SFD·211/221", "P1") with { ControllerId = "P1" } },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Battlefields = ["MARAI"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["MARAI"] = new("P1", "BATTLEFIELD", "MARAI") } };
        var command = Command("ECHO", ManaReduction, "OPTIONAL_COST_REDUCTION:EZREAL-TWO:0:ECHO:MANA");
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana);
        var played = await engine.ResolveAsync(initial, new("marai-component-clamp", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        var paid = Assert.Single(played.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(1, paid.Payload["battlefieldEchoCostReductionMana"]);
        Assert.Equal(1, paid.Payload["optionalAdditionalManaReduction"]);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task OptionalEchoReductionLeavesEveryMandatoryWardFeeDue(int power, bool accepted)
    {
        var initial = Position("SFD·149/221", 3, pool: new(3, power));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TARGET"] = FieldUnit("TARGET", "SFD·037/221", "P2") } };
        var before = MatchStateHasher.Hash(initial);
        var command = Command("ECHO", ManaReduction);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.IsValid, quote.Message);
        Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(2, quote.Cost.GenericPower);
        Assert.Equal(accepted, quote.CanPay);
        var played = await engine.ResolveAsync(initial, new("ward-remains-due", "P1", CommandTypes.PlayCard), command, default);
        Assert.Equal(accepted, played.Accepted);
        if (accepted)
        {
            Assert.Equal(0, played.State.RunePools["P1"].TotalPower);
            Assert.Equal(2, Assert.Single(played.Events, e => e.Kind == "COST_PAID").Payload["spellshieldTaxPower"]);
        }
        else
        {
            Assert.Equal(ErrorCodes.InsufficientCost, played.ErrorCode);
            Assert.Empty(played.Events);
            Assert.Equal(before, MatchStateHasher.Hash(played.State));
        }
    }

    [Theory]
    [InlineData("exhausted")]
    [InlineData("battlefield")]
    [InlineData("borrowed")]
    public async Task PublicControlledPassiveSourceRemainsEligible(string position)
    {
        var initial = Position("SFD·149/221", 3);
        if (position is "exhausted" or "borrowed")
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["EZREAL"] = initial.CardObjects["EZREAL"] with {
                    IsExhausted = position == "exhausted", OwnerId = position == "borrowed" ? "P2" : "P1" } } };
        else
            initial = initial with {
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    ["OWN-FIELD"] = OfficialGarenConquestBattlefieldCountTests.Printed("OWN-FIELD", "OGN·280/298", "P1") with { ControllerId = "P1" } },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    ["P1"] = initial.PlayerZones["P1"] with { Base = [], Battlefields = ["OWN-FIELD", "EZREAL"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                    ["OWN-FIELD"] = new("P1", "BATTLEFIELD", "OWN-FIELD"), ["EZREAL"] = new("P1", "BATTLEFIELD", "OWN-FIELD") } };
        var command = Command("ECHO", ManaReduction);
        var played = await new CoreRuleEngine().ResolveAsync(initial,
            new("passive-source-" + position, "P1", CommandTypes.PlayCard), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
    }

    [Theory]
    [InlineData("SFD·149/221")]
    [InlineData("SFD·149a/221")]
    public async Task ExplicitBlastCrewReductionPaysBeforeAnIndependentEntryTargetIsChosen(string reducer)
    {
        var initial = WithBattlefieldTarget(Position(reducer, 2, "SFD·013/221",
            new(2, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 })));
        var command = new PlayCardCommand("CARD", "SFD·013/221", [], OptionalCosts: ["SPEND_MANA:1", "SPEND_POWER:red:1",
            "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:MANA"]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Red]);
        var played = await engine.ResolveAsync(initial, new("blast-crew-explicit-reduction", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Equal(0, played.State.RunePools["P1"].TotalPower);
        Assert.Equal(["SPEND_MANA:1", "SPEND_POWER:red:1"], Assert.Single(played.State.StackItems).OptionalCosts);
        var targetChoice = Assert.IsType<PendingCardChoiceState>(played.State.PendingCardChoice);
        Assert.Equal(1, targetChoice.RequiredCount);
        Assert.Equal(["TARGET"], targetChoice.LegalObjectIds);
        var confirmed = await engine.ResolveAsync(played.State,
            new("blast-crew-post-entry-target", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(targetChoice.ChoiceId, targetChoice.ChoiceWindow, ["TARGET"]), default);
        Assert.True(confirmed.Accepted, confirmed.ErrorMessage);
        var resolved = await PermanentConfirmationAssert.ResolveAfterPlayAsync(engine, confirmed);
        Assert.Equal(2, resolved.State.CardObjects["TARGET"].Damage);
    }

    [Fact]
    public async Task OptionalPlayReductionCannotBeUsedToPayAnEquipActivation()
    {
        var initial = AddReducer(ArmamentMightTests.Position());
        var before = MatchStateHasher.Hash(initial);
        var valid = await new CoreRuleEngine().ResolveAsync(initial, new("ordinary-equip-fee", "P1", CommandTypes.AssembleEquipment),
            new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_RED"]), default);
        Assert.True(valid.Accepted, valid.ErrorMessage);
        Assert.Equal(initial.RunePools["P1"].TotalPower - 1, valid.State.RunePools["P1"].TotalPower);
        var command = new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_RED", "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:POWER-red"]);
        var played = await new CoreRuleEngine().ResolveAsync(initial, new("not-an-equip-discount", "P1", command.CmdType), command, default);
        Assert.False(played.Accepted);
        Assert.Empty(played.Events);
        Assert.Equal(before, MatchStateHasher.Hash(played.State));
    }

    [Fact]
    public async Task OptionalPlayReductionCannotChangeAnActivatedAbilityFee()
    {
        var initial = Position("SFD·149/221", 0, pool: new(0, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }));
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["OWN-FIELD"] = OfficialGarenConquestBattlefieldCountTests.Printed("OWN-FIELD", "OGN·280/298", "P1") with { ControllerId = "P1" },
                ["ABILITY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ABILITY", P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo, "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Battlefields = ["OWN-FIELD", "ABILITY"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["OWN-FIELD"] = new("P1", "BATTLEFIELD", "OWN-FIELD"), ["ABILITY"] = new("P1", "BATTLEFIELD", "OWN-FIELD") } };
        var command = new ActivateAbilityCommand("ABILITY", P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityId, []);
        var engine = new CoreRuleEngine();
        var valid = await engine.ResolveAsync(initial, new("ordinary-activation-fee", "P1", command.CmdType), command, default);
        Assert.True(valid.Accepted, valid.ErrorMessage);
        Assert.Equal(0, valid.State.RunePools["P1"].TotalPower);
        var before = MatchStateHasher.Hash(initial);
        var forged = await engine.ResolveAsync(initial, new("not-an-activation-discount", "P1", command.CmdType),
            command with { OptionalCosts = ["OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:POWER-blue"] }, default);
        Assert.False(forged.Accepted);
        Assert.Empty(forged.Events);
        Assert.Equal(before, MatchStateHasher.Hash(forged.State));
    }

    [Fact]
    public async Task AnActualIgnoreAllCostsPermissionOffersNoOptionalDiscountOrHastePayment()
    {
        var initial = AddReducer(HookDeckPlayTests.Position("SFD·029/221"));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOOK_TOP"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOOK_TOP", "SFD·029/221", "P1") } };
        var driver = HookDeckPlayTests.Driver(initial, "optional-reductions-ignore-all-costs");
        await HookDeckPlayTests.Activate(driver);
        await driver.Top();
        await DeckPlaySequenceTests.Select(driver, "HOOK_TOP");
        Assert.True(driver.State.PendingEffectPlay!.IgnoreAllCosts);
        var play = Assert.Single(ResolutionResult.BuildPrompts(driver.State)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(play.Metadata!["sourceRequirements"]));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]));
        Assert.False(requirement.ContainsKey("optionalCostReductionGroups"));
        var command = new PlayCardCommand("HOOK_TOP", "SFD·029/221", [], OptionalCosts: [HasteOptionalCostNames.HasteReady,
            "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:POWER-red"]);
        var before = MatchStateHasher.Hash(driver.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(driver.State, new("no-additional-free-play-fee", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        var pool = driver.State.RunePools["P1"];
        await driver.Act("P1", command with { OptionalCosts = [] });
        Assert.Equal(pool, driver.State.RunePools["P1"]);
        Assert.True(driver.State.CardObjects["HOOK_TOP"].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task AComputedReductionRemainsLockedWhenItsSourceIsDestroyedByALaterCost()
    {
        var initial = AddReducer(GrantedOriginHasteTests.Position("SFD·029/221", "OGN·208/298"));
        var driver = await GrantedOriginHasteTests.Reveal(initial, "optional-discount-source-destroyed");
        var command = new PlayCardCommand("ATK_TOP", "OGN·208/298", [], OptionalCosts: ["DESTROY_FRIENDLY_UNIT:EZREAL",
            HasteOptionalCostNames.HasteReady, "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:MANA"]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(4, quote.Cost!.Mana);
        var mana = driver.State.RunePools["P1"].Mana;
        var played = await driver.Act("P1", command);
        Assert.Contains("EZREAL", played.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(mana - 4, played.State.RunePools["P1"].Mana);
        Assert.False(played.State.CardObjects["ATK_TOP"].IsExhausted);
        Assert.Contains(played.Events, ev => ev.Kind == "UNIT_DESTROYED");
        Assert.Equal(["OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:MANA"],
            Assert.IsType<string[]>(Assert.Single(played.Events, ev => ev.Kind == "COST_PAID").Payload["costReductionChoices"]));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task AnActualCopiedEzrealProvidesTheReductionThroughItsOwnCurrentObjectBinding()
    {
        var initial = Position("SFD·149/221", 20);
        initial = initial with {
            CardObjects = initial.CardObjects.Where(pair => pair.Key != "EZREAL").ToDictionary(pair => pair.Key, pair => pair.Value),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Base = [] } },
            ObjectLocations = initial.ObjectLocations.Where(pair => pair.Key != "EZREAL").ToDictionary(pair => pair.Key, pair => pair.Value) };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["TARGET"] = FieldUnit("TARGET", "SFD·149/221", "P2") } };
        initial = OfficialCopyIdentityTests.AddSpell(initial, "COPY", "UNL-200/219");
        var copy = new PlayCardCommand("COPY", "UNL-200/219", ["TARGET"]);
        var engine = new CoreRuleEngine();
        var copyQuote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, copy));
        Assert.True(copyQuote.CanPay, copyQuote.Message);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(copyQuote.Cost!.Mana + 3, 20) } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", copy);
        await driver.Top();
        await driver.Top();
        var copied = driver.State.CardObjects["COPY-TOKEN-001"];
        Assert.Equal("SFD·149/221", copied.CardNo);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, copied.TokenFactoryCardNo);
        Assert.Equal(3, driver.State.RunePools["P1"].Mana);
        var reduction = $"OPTIONAL_COST_REDUCTION:COPY-TOKEN-001:{copied.ObjectGeneration}:ECHO:MANA";
        var command = Command("ECHO", reduction);
        var quote = engine.PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(3, quote.Cost!.Mana);
        await driver.Act("P1", command);
        Assert.Equal(0, driver.State.RunePools["P1"].Mana);
        Assert.Equal(2, Assert.Single(driver.State.StackItems).EffectRepeatCount);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·098/221", "SPEND_MANA:1", "MANA", true)]
    [InlineData("UNL-028/219", "SPEND_POWER:red:1", "POWER-red", false)]
    [InlineData("UNL-028a/219", "SPEND_POWER:red:1", "POWER-red", false)]
    [InlineData("OGN·044/298", "SPEND_POWER:green:1", "POWER-green", false)]
    public async Task OtherOptionalPlayFeesCanBeReducedToZeroWithoutLosingTheirEffects(string card, string fee, string mode, bool mana)
    {
        var initial = WithDrawCard(Position("SFD·149/221", 30, card, new(30, 30)));
        var engine = new CoreRuleEngine();
        var full = new PlayCardCommand("CARD", card, [], OptionalCosts: [fee]);
        var fullQuote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, full));
        Assert.True(fullQuote.CanPay, fullQuote.Message);
        var exactMana = fullQuote.Cost!.Mana - (mana ? 1 : 0);
        var exactPower = fullQuote.Cost.GenericPower + fullQuote.Cost.PowerByTrait.Values.Sum() - (mana ? 0 : 1);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(exactMana, exactPower) } };
        Assert.False(engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, full)).CanPay);
        var reduction = "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:" + mode;
        var command = full with { OptionalCosts = [fee, reduction] };
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(candidate.OptionalCosts!, choice => choice.Id == reduction);
        var played = await engine.ResolveAsync(initial, new("other-optional-fee-" + card, "P1", command.CmdType), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].Mana);
        Assert.Equal(0, played.State.RunePools["P1"].TotalPower);
        Assert.Equal([fee], Assert.IsType<string[]>(Assert.Single(played.Events, ev => ev.Kind == "COST_PAID").Payload["optionalCosts"]));
        var resolved = await PermanentConfirmationAssert.ResolveAfterPlayAsync(engine, played);
        if (card == "SFD·098/221") Assert.Contains(CardObjectTags.Boon, resolved.State.CardObjects["CARD"].Tags);
        else if (card == "OGN·044/298") Assert.Contains("REFILL", resolved.State.PlayerZones["P1"].Hand);
        else
        {
            Assert.False(resolved.State.CardObjects["CARD"].IsExhausted);
            Assert.Equal(2, resolved.State.CardObjects["CARD"].UntilEndOfTurnPowerModifier);
        }
    }

    [Fact]
    public async Task ConditionalReadyFeeUsesTheActualEarlierSpellAndItsReducedPayment()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(WithBattlefieldTarget(Position("SFD·149/221", 20, "UNL-122/219")), "EARLIER", "SFD·066/221");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(6, 1) } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("EARLIER", "SFD·066/221", ["TARGET"]));
        await driver.Top();
        var command = new PlayCardCommand("CARD", "UNL-122/219", [], OptionalCosts: ["SPEND_POWER:purple:1",
            "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:POWER-purple"]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(4, quote.Cost!.Mana);
        await driver.Act("P1", command);
        Assert.False(driver.State.CardObjects["CARD"].IsExhausted);
        Assert.Equal(0, driver.State.RunePools["P1"].Mana);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task OptionalAdditionalReductionCannotReduceBulletTimesResolutionInstructionPayment()
    {
        var initial = WithBattlefieldTarget(Position("SFD·149/221", 20, "OGN·268/298", new(20, 20)));
        var engine = new CoreRuleEngine();
        var full = new PlayCardCommand("CARD", "OGN·268/298", ["FIELD"], OptionalCosts: ["SPEND_POWER:1"]);
        var command = full with { OptionalCosts = ["SPEND_POWER:1", "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:POWER-any"] };
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.DoesNotContain(candidate.OptionalCosts ?? [], choice => choice.Id == "SPEND_POWER:1");
        Assert.DoesNotContain(candidate.OptionalCosts ?? [], choice => choice.Id.StartsWith("OPTIONAL_COST_REDUCTION:", StringComparison.Ordinal));
        Assert.False(engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command)).IsValid);
        var before = MatchStateHasher.Hash(initial);
        var played = await engine.ResolveAsync(initial, new("variable-cost-original-amount", "P1", command.CmdType), command, default);
        Assert.False(played.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, played.ErrorCode);
        Assert.Empty(played.Events);
        Assert.Equal(before, MatchStateHasher.Hash(played.State));
    }

    [Fact]
    public async Task TwoExplicitPowerReductionsStillAuthorizeTheOptionalEquipmentSteal()
    {
        var initial = WithSecondReducer(Position("SFD·149/221", 20, "SFD·109/221", new(20, 20)));
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·022/221", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Base = initial.PlayerZones["P2"].Base.Append("GEAR").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["GEAR"] = new("P2", "BASE") } };
        var engine = new CoreRuleEngine();
        var full = new PlayCardCommand("CARD", "SFD·109/221", [], OptionalCosts: ["AKSHAN_STEAL_EQUIPMENT:GEAR"]);
        var original = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, full));
        Assert.True(original.CanPay, original.Message);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(original.Cost!.Mana, original.Cost.GenericPower + original.Cost.PowerByTrait.Values.Sum() - 2) } };
        var command = full with { OptionalCosts = ["AKSHAN_STEAL_EQUIPMENT:GEAR",
            "OPTIONAL_COST_REDUCTION:EZREAL:0:ADDITIONAL:POWER-orange", "OPTIONAL_COST_REDUCTION:EZREAL-TWO:0:ADDITIONAL:POWER-orange"] };
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        var played = await engine.ResolveAsync(initial, new("free-extra-steal-still-paid", "P1", command.CmdType), command, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(0, played.State.RunePools["P1"].TotalPower);
        var resolved = await PermanentConfirmationAssert.ResolveAfterPlayAsync(engine, played);
        Assert.Equal("P1", resolved.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal("CARD", resolved.State.CardObjects["GEAR"].AttachedToObjectId);
    }

    [Fact]
    public async Task ActuallyAttachedCopiedTextProvidesAnIndependentOptionalCostReduction()
    {
        var (initial, driver) = await OpenAttachedReduction(exactMana: true);
        var engine = new CoreRuleEngine();
        Assert.Equal("SFD·149/221", driver.State.CardObjects["GEAR"].AttachedRuleText!.CardNo);
        Assert.Equal(2, driver.State.RunePools["P1"].Mana);
        Assert.Equal(MatchStatuses.InProgress, driver.State.Status);
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(driver.State)["P1"].Candidates!, item => item.Action == CommandTypes.PlayCard);
        var reductions = (candidate.OptionalCosts ?? []).Where(choice => choice.Label.Contains("减少 1 法力", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, reductions.Length);
        Assert.Equal(2, reductions.Select(choice => choice.Id).Distinct(StringComparer.Ordinal).Count());
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]),
            row => Equals(row["sourceObjectId"], "CARD"));
        var groups = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(requirement["optionalCostReductionGroups"]).ToArray();
        Assert.Equal(2, groups.Length);
        Assert.Equal(2, groups.Select(group => Assert.IsType<string>(group["sourceInstanceId"])).Distinct(StringComparer.Ordinal).Count());
        Assert.Single(groups, group => group["ruleTextOrigin"] is RuleTextInstanceBinding);
        var command = Command(reductions.Select(choice => choice.Id).Prepend("ECHO").ToArray());
        var quote = engine.PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana);
        await driver.Act("P1", command);
        Assert.Equal(0, driver.State.RunePools["P1"].Mana);
        Assert.Equal(2, Assert.Single(driver.State.StackItems).EffectRepeatCount);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DetachedAndReattachedTextNeverRemapsAnOldReductionInstance(bool reattach)
    {
        var (initial, driver) = await OpenAttachedReduction(exactMana: false);
        var oldGroup = CopiedReductionGroup(driver.State);
        var oldChoice = Assert.Single(Assert.IsAssignableFrom<IEnumerable<string>>(oldGroup["choiceIds"]));
        var oldOrigin = Assert.IsType<RuleTextInstanceBinding>(oldGroup["ruleTextOrigin"]);
        var originalGeneration = driver.State.CardObjects["GEAR"].ObjectGeneration;
        await driver.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["EZREAL", "GEAR"]));
        await driver.Top();
        Assert.Null(driver.State.CardObjects["GEAR"].AttachedRuleText);
        if (reattach)
        {
            await driver.Act("P1", new PlayCardCommand("REATTACH", "SFD·011/221", ["EZREAL", "GEAR"]));
            await driver.Top();
            var newOrigin = Assert.IsType<RuleTextInstanceBinding>(CopiedReductionGroup(driver.State)["ruleTextOrigin"]);
            Assert.True(newOrigin.Revision > oldOrigin.Revision);
            Assert.NotEqual(oldChoice, Assert.Single(Assert.IsAssignableFrom<IEnumerable<string>>(CopiedReductionGroup(driver.State)["choiceIds"])));
        }
        Assert.Equal(originalGeneration, driver.State.CardObjects["GEAR"].ObjectGeneration);
        var roundTrip = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(driver.State))!;
        Assert.Equal(MatchStateHasher.Hash(driver.State), MatchStateHasher.Hash(roundTrip));
        var engine = new CoreRuleEngine();
        var cold = new MatchSession(roundTrip, engine, NoopMatchJournal.Instance);
        var stale = Command("ECHO", oldChoice);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(stale, json))!;
        raw["promptId"] = JsonSerializer.SerializeToElement(cold.PromptFor("P1").PromptId);
        raw["snapshotTick"] = JsonSerializer.SerializeToElement(roundTrip.Tick);
        var rejected = await cold.SubmitAsync("P1", "old-text-instance", stale, JsonSerializer.SerializeToElement(raw), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(roundTrip), MatchStateHasher.Hash(rejected.State));
        var currentCosts = new List<string> { "ECHO", ManaReduction };
        if (reattach) currentCosts.Add(Assert.Single(Assert.IsAssignableFrom<IEnumerable<string>>(CopiedReductionGroup(driver.State)["choiceIds"])));
        var valid = await driver.Act("P1", Command(currentCosts.ToArray()));
        Assert.Equal(2, Assert.Single(valid.State.StackItems).EffectRepeatCount);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static IReadOnlyDictionary<string, object?> CopiedReductionGroup(MatchState state)
    {
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, item => item.Action == CommandTypes.PlayCard);
        var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]),
            requirement => Equals(requirement["sourceObjectId"], "CARD"));
        return Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(row["optionalCostReductionGroups"]),
            group => group["ruleTextOrigin"] is RuleTextInstanceBinding);
    }

    private static async Task<(MatchState Initial, StandbyDefendObserverTests.Driver Driver)> OpenAttachedReduction(bool exactMana)
    {
        var initial = WithDrawCard(WithBattlefieldTarget(Position("SFD·149/221", 20, pool: new(20, 20))));
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·059/221", "P1") };
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["GEAR"] = new("P1", "BASE") };
        foreach (var id in new[] { "REFILL-TWO", "REFILL-THREE" })
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·003/298", "P1");
            locations[id] = new("P1", "MAIN_DECK");
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Base = initial.PlayerZones["P1"].Base.Append("GEAR").ToArray(), MainDeck = ["REFILL", "REFILL-TWO", "REFILL-THREE"] } } };
        foreach (var id in new[] { "ATTACH", "DETACH", "REATTACH" })
            initial = OfficialCopyIdentityTests.AddSpell(initial, id, "SFD·011/221");
        var attach = new PlayCardCommand("ATTACH", "SFD·011/221", ["EZREAL", "GEAR"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, attach));
        Assert.True(quote.CanPay, quote.Message);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(exactMana ? quote.Cost!.Mana + 2 : 40, 20) } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", attach);
        await driver.Top();
        Assert.Equal(MatchStatuses.InProgress, driver.State.Status);
        return (initial, driver);
    }

    private static string PrintedEchoReduction(string source, string trait)
        => "OPTIONAL_COST_REDUCTION:" + Uri.EscapeDataString(source) + ":0:ECHO%3AGRANTED%3A1:POWER-" + trait + ":"
            + Uri.EscapeDataString(PrintedPowerCostRules.ChoiceId(new Dictionary<string, int> { [trait] = 1 }));

    private static CardObjectState FieldUnit(string id, string cardNo, string player)
    {
        var printed = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, player);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior));
        return printed with { Tags = printed.Tags.Concat(behavior.SourceUnitTags
            .Split('|', StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.Ordinal).ToArray() };
    }

    private static MatchState WithDrawCard(MatchState state)
        => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["REFILL"] = OfficialGarenConquestBattlefieldCountTests.Printed("REFILL", "OGN·003/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { MainDeck = ["REFILL"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["REFILL"] = new("P1", "MAIN_DECK") } };

    private static MatchState AddReducer(MatchState state)
        => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["EZREAL"] = OfficialGarenConquestBattlefieldCountTests.Printed("EZREAL", "SFD·149/221", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = state.PlayerZones["P1"].Base.Append("EZREAL").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["EZREAL"] = new("P1", "BASE") } };

    private sealed class ReductionJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }

    private static PlayCardCommand Command(params string[] costs)
        => new("CARD", "SFD·066/221", ["TARGET"], OptionalCosts: costs);

    private static MatchState WithSecondReducer(MatchState state)
        => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["EZREAL-TWO"] = state.CardObjects["EZREAL"] with { ObjectId = "EZREAL-TWO" } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = state.PlayerZones["P1"].Base.Append("EZREAL-TWO").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["EZREAL-TWO"] = new("P1", "BASE") }
        };

    private static MatchState WithBattlefieldTarget(MatchState state)
        => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["FIELD"] = OfficialGarenConquestBattlefieldCountTests.Printed("FIELD", "OGN·280/298", "P2") with { ControllerId = "P2" },
                ["TARGET"] = FieldUnit("TARGET", "OGN·142/298", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { Base = [], Battlefields = ["FIELD", "TARGET"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["FIELD"] = new("P2", "BATTLEFIELD", "FIELD"), ["TARGET"] = new("P2", "BATTLEFIELD", "FIELD") }
        };

    private static MatchState Position(string reducer, int mana, string card = "SFD·066/221", RunePool? pool = null)
    {
        var played = OfficialGarenConquestBattlefieldCountTests.Printed("CARD", card, "P1");
        var source = OfficialGarenConquestBattlefieldCountTests.Printed("EZREAL", reducer, "P1");
        var target = OfficialGarenConquestBattlefieldCountTests.Printed("TARGET", "OGN·003/298", "P2");
        return new("OPTIONAL-REDUCTION-ECHO", 0, 3, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = pool ?? new(mana, 0), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = ["CARD"], Base = ["EZREAL"] },
                ["P2"] = PlayerZones.Empty with { Base = ["TARGET"] } },
            cardObjects: new Dictionary<string, CardObjectState> {
                ["CARD"] = played, ["EZREAL"] = source, ["TARGET"] = target },
            objectLocations: new Dictionary<string, ObjectLocationState> {
                ["CARD"] = new("P1", "HAND"), ["EZREAL"] = new("P1", "BASE"), ["TARGET"] = new("P2", "BASE") });
    }
}
