using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadLegendPlayDiscountTests
{
    private const string Legend = "RAD·151/167", Spell = "VEN·049";
    private const string Parent = "USE_PLAY_COST_REDUCTION:", Unit = "PLAY_COST_REDUCTION_UNIT:";
    public static IEnumerable<object[]> Printings() => new[] {
        (Legend, 39659L), ("RAD·174*/167", 39688L), ("RAD·174/167", 39689L),
        ("RAD·174a*/167", 39725L), ("RAD·174a/167", 39726L)
    }.Select(row => new object[] { row.Item1, row.Item2 });

    private static MatchState AddLegend(MatchState s, string id, string no)
        => s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                [id] = new(id, cardNo: no, tags: ["CARD_TYPE:LEGEND"], ownerId: "P1", controllerId: "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { [id] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { LegendZone = [..s.PlayerZones["P1"].LegendZone, id] } }
        };

    private static MatchState Position(string no = Legend, int units = 3, int mana = 30, string spell = Spell)
    {
        var s = OfficialHoldSequenceTests.State("OGN·294/298") with {
            Phase = MatchPhases.Main, TimingState = TimingStates.NeutralOpen, TurnPlayerId = "P1",
            PriorityPlayerId = null, CardPlays = new(5, [], []) };
        s = AddLegend(s, "SINGER", no);
        for (var i = 0; i < units; i++) {
            s = HandDiscardObserverTests.Put(s, "U" + i, "OGN·001/298", "P1", "BASE");
            if (i % 2 == 0) s = LocalDestructionRecallTests.At(s, "U" + i, "P1", "BATTLEFIELD", "F");
        }
        s = HandDiscardObserverTests.Put(s, "SPELL", spell, "P1", "HAND");
        s = HandDiscardObserverTests.Put(s, "FOLLOW", Spell, "P1", "HAND");
        return s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) {
            ["P1"] = new(mana, 30), ["P2"] = new(30, 30) } };
    }

    private static JsonElement? Requirement(MatchState state, string id = "SPELL")
    {
        var prompt = JsonSerializer.SerializeToElement(ResolutionResult.BuildPrompts(state)["P1"], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var candidate in prompt.GetProperty("candidates").EnumerateArray().Where(c => c.GetProperty("action").GetString() == "PLAY_CARD"))
        foreach (var requirement in candidate.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray())
            if (requirement.GetProperty("sourceObjectId").GetString() == id) return requirement;
        return null;
    }

    private static string[] Choices(MatchState state, string id = "SPELL")
        => Requirement(state, id) is { } requirement
            ? requirement.GetProperty("optionalCostChoices").EnumerateArray().Select(c => c.GetProperty("id").GetString()!)
                .Where(id => id.StartsWith(Parent, StringComparison.Ordinal) || id.StartsWith(Unit, StringComparison.Ordinal)).ToArray() : [];

    private static string[] Payment(MatchState state, string id = "SPELL", string source = "SINGER", params string[] units)
    {
        var requirement = Requirement(state, id)!.Value;
        var group = Assert.Single(requirement.GetProperty("optionalPlayCostReductionGroups").EnumerateArray(),
            group => group.GetProperty("sourceObjectId").GetString() == source);
        Assert.Equal(3, group.GetProperty("requiredUnitCount").GetInt32());
        var parent = group.GetProperty("choiceId").GetString()!;
        var support = group.GetProperty("unitChoiceIds").EnumerateArray().Select(c => c.GetString()!).ToArray();
        return new[] { parent }.Concat(units.Length == 0 ? support.Take(3) : units.Select(unit =>
            Assert.Single(support, choice => choice.EndsWith(":" + Uri.EscapeDataString(unit), StringComparison.Ordinal)))).ToArray();
    }

    private static PlayCardCommand Play(string[] costs, string no = Spell, string id = "SPELL")
        => new(id, no, [], OptionalCosts: costs);

    [Theory, MemberData(nameof(Printings))]
    public async Task EveryOfficialPrintingPaysTheLegendAndThreeUnitsBeforeItsNormalResponseWindow(string no, long officialId)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(officialId, card.Id);
        var spec = Assert.IsType<OptionalPlayCostReductionSpec>(OptionalPlayCostReductionParser.Parse(card.CardEffect));
        Assert.Equal("SPELL", spec.Category); Assert.Equal(2, spec.Mana); Assert.Equal(0, spec.Power);
        Assert.True(spec.ExhaustSource); Assert.False(spec.OncePerTurn); Assert.Equal(3, spec.SupportingUnitCount);
        var initial = Position(no, mana: 0); var payment = Payment(initial); var command = Play(payment);
        var hash = MatchStateHasher.Hash(initial);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(hash, MatchStateHasher.Hash(initial));
        var game = new Driver(initial); await game.Act("P1", command);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(30, game.State.RunePools["P1"].TotalPower);
        Assert.All(new[] { "SINGER", "U0", "U1", "U2" }, id => Assert.True(game.State.CardObjects[id].IsExhausted));
        Assert.Single(game.State.StackItems); Assert.Empty(Choices(game.State, "FOLLOW"));
        Assert.Single(game.Events(), e => e.Event.Kind == "LEGEND_EXHAUSTED");
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_EXHAUSTED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "SINGER"));
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith("OPTIONAL_PLAY_COST_REDUCTION_USED|", StringComparison.Ordinal));
        await game.Top(); Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DRAWN"); await game.Replay();
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task EveryPrintingRequiresAllThreeDistinctActiveFriendlyUnits(string no, long _)
    {
        var initial = Position(no); var payment = Payment(initial);
        await Reject(initial, Play(payment.Take(3).ToArray()));
        var exhausted = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["U0"] = initial.CardObjects["U0"] with { IsExhausted = true } } };
        Assert.Empty(Choices(exhausted)); await Reject(exhausted, Play(payment));
    }

    [Fact]
    public async Task DecliningTheReductionPaysFullPriceAndDoesNotExhaustAnything()
    {
        var game = new Driver(Position()); var payment = Payment(game.State);
        await game.Act("P1", Play([])); Assert.Equal(28, game.State.RunePools["P1"].Mana);
        Assert.All(new[] { "SINGER", "U0", "U1", "U2" }, id => Assert.False(game.State.CardObjects[id].IsExhausted));
        await game.Top(); Assert.Equal(payment, Payment(game.State, "FOLLOW")); await game.Replay();
    }

    [Theory]
    [InlineData("legend-exhausted")] [InlineData("legend-hidden")] [InlineData("legend-enemy")]
    [InlineData("unit-hidden")] [InlineData("unit-enemy")] [InlineData("unit-equipment")] [InlineData("unit-rune")]
    public async Task StaleChoicesCannotSpendUnavailableSourcesOrNonUnitSupport(string branch)
    {
        var state = Position(); var payment = Payment(state);
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects);
        if (branch == "legend-exhausted") cards["SINGER"] = cards["SINGER"] with { IsExhausted = true };
        if (branch == "legend-hidden") cards["SINGER"] = cards["SINGER"] with { IsFaceDown = true };
        if (branch == "legend-enemy") cards["SINGER"] = cards["SINGER"] with { ControllerId = "P2" };
        if (branch == "unit-hidden") cards["U0"] = cards["U0"] with { IsFaceDown = true };
        if (branch == "unit-equipment") cards["U0"] = OfficialGarenConquestBattlefieldCountTests.Printed("U0", "SFD·016/221", "P1");
        if (branch == "unit-rune") cards["U0"] = cards["U0"] with { Tags = [CardObjectTags.RuneCard] };
        state = state with { CardObjects = cards };
        if (branch == "unit-enemy") state = LocalDestructionRecallTests.At(state, "U0", "P2", "BATTLEFIELD", "OTHER");
        Assert.Empty(Choices(state)); await Reject(state, Play(payment));
    }

    [Theory]
    [InlineData("missing-parent")] [InlineData("missing-unit")] [InlineData("extra-unit")]
    [InlineData("duplicate-unit")] [InlineData("duplicate-parent")] [InlineData("forged-unit")]
    public async Task ForgedOrIncorrectCostGroupsAreAtomicRejections(string branch)
    {
        var state = Position(units: 4); var payment = Payment(state);
        var costs = branch switch {
            "missing-parent" => payment.Skip(1).ToArray(), "missing-unit" => payment.Take(3).ToArray(),
            "extra-unit" => [..payment, Assert.Single(Choices(state).Except(payment))],
            "duplicate-unit" => [..payment, payment[1]], "duplicate-parent" => [..payment, payment[0]],
            _ => [payment[0], payment[1], payment[2], Unit + "invented:U2"]
        };
        await Reject(state, Play(costs));
    }

    [Theory]
    [InlineData("SFD·016/221", "BASE")] [InlineData("OGN·001/298", "BASE")]
    public async Task TheLegendCannotReduceEquipmentOrUnitPlays(string card, string destination)
    {
        var state = Position(); var stale = Payment(state);
        state = HandDiscardObserverTests.Put(state, "OTHER-PLAY", card, "P1", "HAND");
        Assert.Empty(Choices(state, "OTHER-PLAY"));
        await Reject(state, new("OTHER-PLAY", card, [], OptionalCosts: stale, Destination: destination));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheSpellsOwnExhaustCostMustUseADifferentUnit(bool distinct)
    {
        const string meditation = "OGN·048/298";
        var initial = Position(units: 4, spell: meditation);
        var costs = Payment(initial, units: ["U0", "U1", "U2"])
            .Append("EXHAUST_FRIENDLY_UNIT:" + (distinct ? "U3" : "U2")).ToArray();
        if (!distinct) { await Reject(initial, Play(costs, meditation)); return; }
        var game = new Driver(initial); await game.Act("P1", Play(costs, meditation)); await game.Top();
        Assert.All(new[] { "U0", "U1", "U2", "U3" }, id => Assert.True(game.State.CardObjects[id].IsExhausted));
        Assert.Equal(2, game.Events().Where(e => e.Event.Kind == "CARD_DRAWN").Sum(e => Convert.ToInt32(e.Event.Payload["count"])));
        await game.Replay();
    }

    [Fact]
    public async Task CounteringTheSpellDoesNotRefundTheLegendOrUnitCosts()
    {
        var initial = HandDiscardObserverTests.Put(Position(), "COUNTER", "OGN·064/298", "P2", "HAND");
        var game = new Driver(initial); await game.Act("P1", Play(Payment(initial)));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [Assert.Single(game.State.StackItems).StackItemId]));
        await game.Top(); Assert.Empty(game.State.StackItems);
        Assert.All(new[] { "SINGER", "U0", "U1", "U2" }, id => Assert.True(game.State.CardObjects[id].IsExhausted));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN"); await game.Replay();
    }

    [Fact]
    public async Task ARealRoyalAttendantCanReadyTheLegendForAnotherReductionInTheSameTurn()
    {
        var initial = HandDiscardObserverTests.Put(Position(units: 6), "ATTENDANT", "SFD·039/221", "P1", "HAND");
        var game = new Driver(initial); await game.Act("P1", Play(Payment(initial, units: ["U0", "U1", "U2"]))); await game.Top();
        await game.Act("P1", new PlayCardCommand("ATTENDANT", "SFD·039/221", ["SINGER"], Mode: "READY_LEGEND", Destination: "BASE"));
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.False(game.State.CardObjects["SINGER"].IsExhausted);
        await game.Act("P1", Play(Payment(game.State, "FOLLOW", units: ["U3", "U4", "U5"]), id: "FOLLOW")); await game.Top();
        Assert.Equal(initial.TurnNumber, game.State.TurnNumber);
        Assert.Equal(27, game.State.RunePools["P1"].Mana);
        Assert.All(Enumerable.Range(0, 6).Select(i => "U" + i), id => Assert.True(game.State.CardObjects[id].IsExhausted));
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "LEGEND_EXHAUSTED")); await game.Replay();
    }

    [Fact]
    public async Task TwoSourcesCannotShareTheSameSupportingUnits()
    {
        var initial = AddLegend(Position(units: 6), "SECOND", "RAD·174/167");
        var first = Payment(initial, units: ["U0", "U1", "U2"]);
        var duplicate = Payment(initial, source: "SECOND", units: ["U0", "U1", "U2"]);
        await Reject(initial, Play([..first, ..duplicate]));
        var second = Payment(initial, source: "SECOND", units: ["U3", "U4", "U5"]);
        var game = new Driver(initial); await game.Act("P1", Play([..first, ..second]));
        Assert.Equal(30, game.State.RunePools["P1"].Mana); Assert.Equal(30, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["SINGER"].IsExhausted); Assert.True(game.State.CardObjects["SECOND"].IsExhausted);
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(5, 2)] [InlineData(6, 0)]
    public void PromptAffordabilityDoesNotCountTheSameUnitBudgetForTwoSources(int units, int expectedMana)
    {
        var state = AddLegend(Position(units: units, spell: "OGN·083/298"), "SECOND", "RAD·174/167");
        Assert.Equal(expectedMana, Requirement(state)!.Value.GetProperty("minimumManaCost").GetInt32());
    }

    [Theory]
    [InlineData("三", "装备")] [InlineData("一二", "法术")] [InlineData("0", "法术")]
    public void UnsupportedOrInconsistentTextDoesNotGrantAnExhaustionDiscount(string count, string noun)
        => Assert.Null(OptionalPlayCostReductionParser.Parse(
            $"你可以选择让我及{count}名友方单位变为休眠状态，作为打出一个法术的额外费用，以此让该{noun}的费用减少{{{{2}}}}。"));

    [Fact]
    public void RecoveryRejectsAnImpossibleOncePerTurnUseRecordForTheLegend()
    {
        var state = Position();
        var use = new OptionalPlayCostReductionUse(new("SINGER", 0), Legend, "PRINTED:SINGER:0", 0, 1, state.TurnNumber, "P1");
        var marker = "OPTIONAL_PLAY_COST_REDUCTION_USED|" + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(use));
        Assert.Contains("invalid optional play cost reduction use", OfficialInsightAndSpellLockTests.Errors(
            state with { Tick = 1, UntilEndOfTurnEffects = [marker] }));
    }

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.IsValid);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-legend-discount", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
