using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Explicit midgame fixtures: actual attack/reveal grants an outside-hand play.
// The spell prerequisite is earned by a real command, never a fabricated marker.
public sealed class CompoundOptionalCostTests
{
    private const string Source = "ATK_TOP";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("SFD·098/221", 0)]
    [InlineData("SFD·098/221", 1)]
    [InlineData("SFD·098/221", 2)]
    [InlineData("SFD·098/221", 3)]
    [InlineData("SFD·098/221", 4)]
    [InlineData("OGN·044/298", 0)]
    [InlineData("OGN·044/298", 1)]
    [InlineData("OGN·044/298", 2)]
    [InlineData("OGN·044/298", 3)]
    [InlineData("OGN·044/298", 4)]
    [InlineData("UNL-028/219", 0)]
    [InlineData("UNL-028/219", 1)]
    [InlineData("UNL-028/219", 2)]
    [InlineData("UNL-028/219", 3)]
    [InlineData("UNL-028/219", 4)]
    [InlineData("UNL-122/219", 0)]
    [InlineData("UNL-122/219", 1)]
    [InlineData("UNL-122/219", 2)]
    [InlineData("UNL-122/219", 3)]
    [InlineData("UNL-122/219", 4)]
    public async Task GrantedHasteAndTheUnitsOwnFeeAreIndependentlyOptionalAndComposeInEitherOrder(string card, int mode)
    {
        var initial = Position(card);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Open(driver, card == "UNL-122/219");
        var haste = mode is 1 or 3 or 4;
        var own = mode is 2 or 3 or 4;
        var costs = new List<string>();
        if (haste) costs.Add(HasteOptionalCostNames.HasteReady);
        if (own) costs.Add(OwnFee(card));
        if (mode == 4) costs.Reverse();
        var command = new PlayCardCommand(Source, card, [], OptionalCosts: costs);
        var before = driver.State;
        var quote = new CoreRuleEngine().PreviewPlayCard(before, "P1", PlayCostPreviewTests.Request(before, command));
        await RetainRejectedOriginal(card + "-" + mode, before, command, quote, driver);
        Assert.True(quote.CanPay, quote.Message);
        var mana = (card == "UNL-122/219" ? 4 : card == "UNL-028/219" ? 3 : 2)
            + (haste ? 1 : 0) + (own && card == "SFD·098/221" ? 1 : 0);
        var power = (haste ? 1 : 0) + (own && card != "SFD·098/221" ? 1 : 0);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(power, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var paid = await driver.Act("P1", command);
        Assert.Equal(before.RunePools["P1"].Mana - mana, driver.State.RunePools["P1"].Mana);
        Assert.Equal(before.RunePools["P1"].TotalPower - power, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(costs, Assert.IsType<string[]>(Assert.Single(paid.Events, e => e.Kind == "COST_PAID").Payload["optionalCosts"]));
        var entered = Assert.Single(paid.Events, e => e.Kind == "UNIT_PLAYED_TO_BASE");
        Assert.Equal(haste, entered.Payload.TryGetValue("hasteReadyOptionalCostPaid", out var ready) && Equals(ready, true));
        for (var i = 0; i < 3 && driver.State.StackItems.FirstOrDefault()?.SourceObjectId == Source; i++) await driver.Top();
        Assert.Equal(!(haste || own && card is "UNL-028/219" or "UNL-122/219"), driver.State.CardObjects[Source].IsExhausted);
        Assert.Equal(card == "UNL-122/219" ? 4 : 2 + (own && card == "UNL-028/219" ? 2 : own && card == "SFD·098/221" ? 1 : 0),
            driver.State.CardObjects[Source].Power);
        Assert.Equal(before.PlayerZones["P1"].Hand.Count + (own && card == "OGN·044/298" ? 1 : 0), driver.State.PlayerZones["P1"].Hand.Count);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        Export(card + "-" + mode, new { quote, commands = driver.Commands(), events = driver.Events(),
            finalHash = MatchStateHasher.Hash(driver.State), fullOriginalReplayPassed = true });
    }

    private static MatchState Position(string card)
    {
        var state = GrantedOriginHasteTests.Position("SFD·029/221", card);
        state = AttackDamageDistributionTests.Spell(state, "PREREQUISITE", "OGN·057/298", "P1");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(30, 0, new Dictionary<string, int> {
                [RuneTrait.Red] = 20, [RuneTrait.Green] = 20, [RuneTrait.Blue] = 20,
                [RuneTrait.Yellow] = 20, [RuneTrait.Orange] = 20, [RuneTrait.Purple] = 20 }) } };
    }

    [Theory]
    [InlineData("SFD·098/221", false)]
    [InlineData("SFD·098/221", true)]
    [InlineData("OGN·044/298", false)]
    [InlineData("OGN·044/298", true)]
    [InlineData("UNL-028/219", false)]
    [InlineData("UNL-028/219", true)]
    [InlineData("UNL-122/219", false)]
    [InlineData("UNL-122/219", true)]
    public async Task OneActualEzrealMayReduceBothIndependentFeesWithMatchingPromptGroups(string card, bool hasteMana)
    {
        var initial = WithReducer(Position(card), "EZREAL");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Open(driver, card == "UNL-122/219");
        var trait = Trait(card);
        var hasteReduction = Reduction("EZREAL", "HASTE_READY", hasteMana ? "MANA" : "POWER-" + trait);
        var ownReduction = Reduction("EZREAL", "ADDITIONAL", card == "SFD·098/221" ? "MANA" : "POWER-" + trait);
        var command = new PlayCardCommand(Source, card, [], OptionalCosts:
            [HasteOptionalCostNames.HasteReady, OwnFee(card), hasteReduction, ownReduction]);
        var before = driver.State;
        var quote = new CoreRuleEngine().PreviewPlayCard(before, "P1", PlayCostPreviewTests.Request(before, command));
        await RetainRejectedOriginal("reduced-" + card + "-" + hasteMana, before, command, quote, driver);
        Assert.True(quote.CanPay, quote.Message);
        var requirement = Requirement(before);
        var choices = requirement.GetProperty("optionalCostChoices").EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToArray();
        Assert.Contains(hasteReduction, choices); Assert.Contains(ownReduction, choices);
        var groups = requirement.GetProperty("optionalCostReductionGroups").EnumerateArray().ToArray();
        foreach (var (component, selected, required) in new[] {
            ("HASTE_READY", hasteReduction, HasteOptionalCostNames.HasteReady), ("ADDITIONAL", ownReduction, OwnFee(card)) })
        {
            var group = Assert.Single(groups, g => g.GetProperty("componentId").GetString() == component);
            Assert.Equal(1, group.GetProperty("maxSelections").GetInt32());
            Assert.Contains(selected, group.GetProperty("choiceIds").EnumerateArray().Select(x => x.GetString()));
            var selection = Assert.Single(group.GetProperty("requiresOneOfOptionalCostSelections").EnumerateArray());
            Assert.Equal([required], selection.EnumerateArray().Select(x => x.GetString()));
        }
        var baseMana = card == "UNL-122/219" ? 4 : card == "UNL-028/219" ? 3 : 2;
        Assert.Equal(baseMana + (hasteMana ? 0 : 1), quote.Cost!.Mana);
        Assert.Equal(hasteMana ? 1 : 0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var paid = await driver.Act("P1", command);
        Assert.Equal(quote.Cost.RemainingMana, driver.State.RunePools["P1"].Mana);
        Assert.Equal(quote.Cost.RemainingPowerByTrait!.OrderBy(x => x.Key), driver.State.RunePools["P1"].PowerByTrait!.OrderBy(x => x.Key));
        var payment = Assert.Single(paid.Events, e => e.Kind == "COST_PAID");
        Assert.Equal([hasteReduction, ownReduction], Assert.IsType<string[]>(payment.Payload["costReductionChoices"]));
        var receipts = JsonSerializer.SerializeToElement(payment.Payload["optionalCostReductions"], Json).EnumerateArray().ToArray();
        Assert.Equal(["HASTE_READY", "ADDITIONAL"], receipts.Select(r => r.GetProperty("componentId").GetString()));
        for (var i = 0; i < 3 && driver.State.StackItems.FirstOrDefault()?.SourceObjectId == Source; i++) await driver.Top();
        Assert.False(driver.State.CardObjects[Source].IsExhausted);
        if (card == "OGN·044/298") Assert.Equal(before.PlayerZones["P1"].Hand.Count + 1, driver.State.PlayerZones["P1"].Hand.Count);
        if (card == "SFD·098/221") Assert.Equal(3, driver.State.CardObjects[Source].Power);
        if (card == "UNL-028/219") Assert.Equal(4, driver.State.CardObjects[Source].Power);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        Export("reduced-" + card + "-" + hasteMana, new { requirement, quote, commands = driver.Commands(), events = driver.Events(),
            finalHash = MatchStateHasher.Hash(driver.State), fullOriginalReplayPassed = true });
    }

    [Fact]
    public async Task ExcessReductionsStopAtEachFeeAndNeverReduceTheUnitsBaseMana()
    {
        const string card = "SFD·098/221";
        var initial = WithReducer(WithReducer(Position(card), "EZREAL"), "EZREAL-TWO");
        var driver = new StandbyDefendObserverTests.Driver(initial); await Open(driver, false);
        var command = new PlayCardCommand(Source, card, [], OptionalCosts: [HasteOptionalCostNames.HasteReady, OwnFee(card),
            Reduction("EZREAL", "HASTE_READY", "MANA"), Reduction("EZREAL-TWO", "HASTE_READY", "MANA"),
            Reduction("EZREAL", "ADDITIONAL", "MANA"), Reduction("EZREAL-TWO", "ADDITIONAL", "MANA")]);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        await RetainRejectedOriginal("per-fee-floor", driver.State, command, quote, driver);
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Orange]);
        var paid = await driver.Act("P1", command);
        Assert.Equal(2, Assert.Single(paid.Events, e => e.Kind == "COST_PAID").Payload["optionalAdditionalManaReduction"]);
        for (var i = 0; i < 3 && driver.State.StackItems.FirstOrDefault()?.SourceObjectId == Source; i++) await driver.Top();
        Assert.False(driver.State.CardObjects[Source].IsExhausted);
        Assert.Equal(3, driver.State.CardObjects[Source].Power);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        Export("per-fee-floor", new { quote, commands = driver.Commands(), events = driver.Events(),
            finalHash = MatchStateHasher.Hash(driver.State), fullOriginalReplayPassed = true });
    }

    [Theory]
    [InlineData("duplicate-haste")]
    [InlineData("duplicate-own")]
    [InlineData("unknown")]
    [InlineData("wrong-trait")]
    [InlineData("unmet-condition")]
    [InlineData("no-grant")]
    [InlineData("underfunded")]
    [InlineData("duplicate-reducer")]
    [InlineData("missing-fee")]
    [InlineData("foreign-component")]
    [InlineData("stale-generation")]
    public async Task InvalidCompoundsNeverDebitOrGrantEitherEffect(string branch)
    {
        var card = branch == "unmet-condition" ? "UNL-122/219" : "OGN·044/298";
        var initial = WithReducer(Position(card), "EZREAL");
        if (branch == "no-grant") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HASTE-GRANT"] = initial.CardObjects["HASTE-GRANT"] with { CardNo = "OGN·096/298", Power = 1, ManaCost = 2 } } };
        if (branch == "underfunded") initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(3, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 }) } };
        var driver = new StandbyDefendObserverTests.Driver(initial); await Open(driver, false);
        var costs = new List<string> { HasteOptionalCostNames.HasteReady, OwnFee(card) };
        if (branch == "duplicate-haste") costs.Add(HasteOptionalCostNames.HasteReady);
        if (branch == "duplicate-own") costs.Add(OwnFee(card));
        if (branch == "unknown") costs.Add("SPEND_MANA:999");
        if (branch == "wrong-trait") costs[1] = "SPEND_POWER:blue:1";
        if (branch == "duplicate-reducer") costs.AddRange([
            Reduction("EZREAL", "HASTE_READY", "MANA"), Reduction("EZREAL", "HASTE_READY", "POWER-green")]);
        if (branch == "missing-fee") { costs.Remove(OwnFee(card)); costs.Add(Reduction("EZREAL", "ADDITIONAL", "POWER-green")); }
        if (branch == "foreign-component") costs.Add(Reduction("EZREAL", "ECHO", "MANA"));
        if (branch == "stale-generation") costs.Add("OPTIONAL_COST_REDUCTION:EZREAL:1:HASTE_READY:MANA");
        var command = new PlayCardCommand(Source, card, [], OptionalCosts: costs);
        var before = MatchStateHasher.Hash(driver.State);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.False(quote.CanPay);
        await RetainRejectedOriginal("rejected-" + branch, driver.State, command, quote, driver);
        Assert.Equal(before, MatchStateHasher.Hash(driver.State));
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "UNIT_PLAYED_TO_BASE");
        if (branch == "underfunded") { Assert.True(quote.IsValid); Assert.Equal(1, quote.Cost!.MissingPower); }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static MatchState WithReducer(MatchState state, string id)
        => LocalDestructionRecallTests.At(OfficialHoldSequenceTests.AddUnit(state, id, "SFD·149/221", "F", "P1"), id, "P1", "BASE");

    [Theory]
    [InlineData("zero-reduced")]
    [InlineData("two-runes")]
    [InlineData("one-rune")]
    public async Task CombinedFeesHonorTheRealZeroPoolAndAtomicInlineRuneBoundary(string branch)
    {
        const string card = "OGN·044/298";
        var initial = Position(card);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(3, 0) } };
        if (branch == "zero-reduced") initial = WithReducer(initial, "EZREAL");
        else
        {
            var objects = new Dictionary<string, CardObjectState>(initial.CardObjects);
            var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations);
            foreach (var id in new[] { "COMPOUND-RUNE-1", "COMPOUND-RUNE-2" })
            {
                objects[id] = new(id, cardNo: "UNL-R02", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:green"]);
                locations[id] = new("P1", "BASE");
            }
            initial = initial with { CardObjects = objects, ObjectLocations = locations,
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "COMPOUND-RUNE-1", "COMPOUND-RUNE-2"] } } };
        }
        var driver = new StandbyDefendObserverTests.Driver(initial); await Open(driver, false);
        string[] costs = [HasteOptionalCostNames.HasteReady, OwnFee(card)];
        costs = branch == "zero-reduced" ? [..costs, Reduction("EZREAL", "HASTE_READY", "POWER-green"), Reduction("EZREAL", "ADDITIONAL", "POWER-green")]
            : branch == "two-runes" ? [..costs, "RECYCLE_RUNE:COMPOUND-RUNE-1", "RECYCLE_RUNE:COMPOUND-RUNE-2"]
            : [..costs, "RECYCLE_RUNE:COMPOUND-RUNE-1"];
        var command = new PlayCardCommand(Source, card, [], OptionalCosts: costs);
        var before = driver.State;
        var quote = new CoreRuleEngine().PreviewPlayCard(before, "P1", PlayCostPreviewTests.Request(before, command));
        await RetainRejectedOriginal("boundary-" + branch, before, command, quote, driver);
        Assert.True(quote.IsValid, quote.Message); Assert.Equal(branch != "one-rune", quote.CanPay);
        if (branch == "one-rune")
        {
            Assert.Equal(1, quote.Cost!.MissingPower);
            Assert.Contains("COMPOUND-RUNE-1", driver.State.PlayerZones["P1"].Base);
            Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(driver.State));
        }
        else
        {
            var paid = await driver.Act("P1", command);
            Assert.Equal(0, driver.State.RunePools["P1"].Mana); Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
            Assert.Equal(quote.Cost!.RemainingMana, driver.State.RunePools["P1"].Mana);
            Assert.False(driver.State.CardObjects[Source].IsExhausted);
            if (branch == "two-runes") foreach (var id in new[] { "COMPOUND-RUNE-1", "COMPOUND-RUNE-2" }) Assert.Contains(id, driver.State.PlayerZones["P1"].RuneDeck);
            else Assert.Equal(2, Assert.Single(paid.Events, e => e.Kind == "COST_PAID").Payload["optionalAdditionalPowerReduction"]);
            for (var i = 0; i < 3 && driver.State.StackItems.FirstOrDefault()?.SourceObjectId == Source; i++) await driver.Top();
            Assert.Equal(before.PlayerZones["P1"].Hand.Count + 1, driver.State.PlayerZones["P1"].Hand.Count);
        }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        if (branch != "one-rune") Export("boundary-" + branch, new { quote, commands = driver.Commands(), events = driver.Events(),
            finalHash = MatchStateHasher.Hash(driver.State), fullOriginalReplayPassed = true });
    }

    [Theory]
    [InlineData("SFD·098/221", false)]
    [InlineData("SFD·098/221", true)]
    [InlineData("OGN·044/298", false)]
    [InlineData("OGN·044/298", true)]
    [InlineData("UNL-028/219", false)]
    [InlineData("UNL-028/219", true)]
    [InlineData("UNL-122/219", false)]
    [InlineData("UNL-122/219", true)]
    public async Task AReducerUsesTheSameComponentWhenOnlyOneOfTheFeesIsChosen(string card, bool haste)
    {
        var initial = WithReducer(Position(card), "EZREAL");
        var driver = new StandbyDefendObserverTests.Driver(initial); await Open(driver, card == "UNL-122/219");
        var component = haste ? "HASTE_READY" : "ADDITIONAL";
        var selected = haste ? HasteOptionalCostNames.HasteReady : OwnFee(card);
        var reduction = Reduction("EZREAL", component, !haste && card == "SFD·098/221" ? "MANA" : "POWER-" + Trait(card));
        var command = new PlayCardCommand(Source, card, [], OptionalCosts: [selected, reduction]);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        await RetainRejectedOriginal("single-reduced-" + card + "-" + haste, driver.State, command, quote, driver);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal((card == "UNL-122/219" ? 4 : card == "UNL-028/219" ? 3 : 2) + (haste ? 1 : 0), quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var requirement = Requirement(driver.State);
        var group = Assert.Single(requirement.GetProperty("optionalCostReductionGroups").EnumerateArray(), g => g.GetProperty("componentId").GetString() == component);
        Assert.Contains(reduction, group.GetProperty("choiceIds").EnumerateArray().Select(x => x.GetString()));
        var paid = await driver.Act("P1", command);
        Assert.Equal(quote.Cost.RemainingMana, driver.State.RunePools["P1"].Mana);
        var receipt = Assert.Single(JsonSerializer.SerializeToElement(Assert.Single(paid.Events, e => e.Kind == "COST_PAID").Payload["optionalCostReductions"], Json).EnumerateArray());
        Assert.Equal(component, receipt.GetProperty("componentId").GetString());
        for (var i = 0; i < 3 && driver.State.StackItems.FirstOrDefault()?.SourceObjectId == Source; i++) await driver.Top();
        Assert.Equal(!(haste || card is "UNL-028/219" or "UNL-122/219"), driver.State.CardObjects[Source].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        Export("single-reduced-" + card + "-" + haste, new { requirement, quote, commands = driver.Commands(), events = driver.Events(),
            finalHash = MatchStateHasher.Hash(driver.State), fullOriginalReplayPassed = true });
    }

    [Theory]
    [InlineData("SFD·013/221", false)]
    [InlineData("SFD·013/221", true)]
    [InlineData("SFD·067/221", false)]
    [InlineData("SFD·067/221", true)]
    [InlineData("SFD·109/221", false)]
    [InlineData("SFD·109/221", true)]
    public async Task TargetedAndEquipmentFeesComposeWithHasteAsOneIndependentPrintedFee(string card, bool reduce)
    {
        var initial = WithReducer(Position(card), "EZREAL");
        const string gear = "ENEMY-GEAR";
        if (card == "SFD·109/221") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [gear] = new(gear, cardNo: "SFD·042/221", manaCost: 2, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.EquipmentCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [gear] = new("P2", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { Base = [..initial.PlayerZones["P2"].Base, gear] } } };
        var driver = new StandbyDefendObserverTests.Driver(initial); await Open(driver, false);
        var trait = card == "SFD·013/221" ? RuneTrait.Red : card == "SFD·067/221" ? RuneTrait.Blue : RuneTrait.Orange;
        string[] own = card == "SFD·013/221" ? ["SPEND_MANA:1", "SPEND_POWER:red:1"]
            : card == "SFD·067/221" ? ["SPEND_POWER:blue:1"] : ["AKSHAN_STEAL_EQUIPMENT:" + gear];
        string[] costs = [HasteOptionalCostNames.HasteReady, ..own];
        if (reduce) costs = [..costs, Reduction("EZREAL", "HASTE_READY", "MANA"), Reduction("EZREAL", "ADDITIONAL", "POWER-" + trait)];
        var command = new PlayCardCommand(Source, card, card == "SFD·109/221" ? [] : ["HOST"], OptionalCosts: costs);
        var before = driver.State;
        var quote = new CoreRuleEngine().PreviewPlayCard(before, "P1", PlayCostPreviewTests.Request(before, command));
        await RetainRejectedOriginal("targeted-" + card + "-" + reduce, before, command, quote, driver);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal((card == "SFD·013/221" ? 4 : card == "SFD·067/221" ? 4 : 5) - (reduce ? 1 : 0), quote.Cost!.Mana);
        Assert.Equal((card == "SFD·109/221" ? 3 : 2) - (reduce ? 1 : 0), quote.Cost.PowerByTrait[trait]);
        var requirement = Requirement(before);
        var paid = await driver.Act("P1", command);
        Assert.Equal(quote.Cost.RemainingMana, driver.State.RunePools["P1"].Mana);
        Assert.Equal(quote.Cost.RemainingPowerByTrait!.OrderBy(x => x.Key), driver.State.RunePools["P1"].PowerByTrait!.OrderBy(x => x.Key));
        if (driver.State.PendingCardChoice is { ChoiceWindow: "TEMPERED_CONFIRMATION" } tempered)
            await driver.Act(tempered.PlayerId, new ChooseCardsCommand(tempered.ChoiceId, tempered.ChoiceWindow, []));
        for (var i = 0; i < 3 && driver.State.StackItems.FirstOrDefault()?.SourceObjectId == Source; i++) await driver.Top();
        Assert.False(driver.State.CardObjects[Source].IsExhausted);
        if (card == "SFD·013/221") Assert.Equal(before.CardObjects["HOST"].Damage + 2, driver.State.CardObjects["HOST"].Damage);
        if (card == "SFD·067/221") Assert.Equal(before.CardObjects["HOST"].Power - 2, driver.State.CardObjects["HOST"].Power);
        if (card == "SFD·109/221") { Assert.Equal("P1", driver.State.CardObjects[gear].ControllerId); Assert.Contains(gear, driver.State.PlayerZones["P1"].Base); }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        Export("targeted-" + card + "-" + reduce, new { requirement, quote, commands = driver.Commands(), events = driver.Events(),
            finalHash = MatchStateHasher.Hash(driver.State), fullOriginalReplayPassed = true });
    }

    private static string Trait(string card) => card switch {
        "SFD·098/221" => RuneTrait.Orange, "OGN·044/298" => RuneTrait.Green,
        "UNL-028/219" => RuneTrait.Red, "UNL-122/219" => RuneTrait.Purple,
        _ => throw new ArgumentOutOfRangeException(nameof(card)) };

    [Theory]
    [InlineData("SFD·013/221", "duplicate-haste")]
    [InlineData("SFD·013/221", "wrong-haste")]
    [InlineData("SFD·013/221", "unknown-haste")]
    [InlineData("SFD·013/221", "unrelated-fee")]
    [InlineData("SFD·013/221", "missing-fee")]
    [InlineData("SFD·013/221", "unconfirmed")]
    [InlineData("SFD·013/221", "forged-paid")]
    [InlineData("SFD·013/221", "forged-origin")]
    [InlineData("SFD·067/221", "duplicate-haste")]
    [InlineData("SFD·067/221", "wrong-haste")]
    [InlineData("SFD·067/221", "unknown-haste")]
    [InlineData("SFD·067/221", "unrelated-fee")]
    [InlineData("SFD·067/221", "missing-fee")]
    [InlineData("SFD·067/221", "unconfirmed")]
    [InlineData("SFD·067/221", "forged-paid")]
    [InlineData("SFD·067/221", "forged-origin")]
    public async Task CoordinatedFramesKeepStrictCostStructureAndOriginalPaymentAuthority(string card, string mutation)
    {
        var initial = Position(card);
        var driver = new StandbyDefendObserverTests.Driver(initial); await Open(driver, false);
        string[] own = card == "SFD·013/221" ? ["SPEND_MANA:1", "SPEND_POWER:red:1"] : ["SPEND_POWER:blue:1"];
        var haste = mutation != "forged-paid";
        await driver.Act("P1", new PlayCardCommand(Source, card, [], OptionalCosts: haste ? [HasteOptionalCostNames.HasteReady, ..own] : own));
        var real = Frame(initial, driver, driver.State);
        Assert.Empty(real.ValidationErrors);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, real.Commands, driver.State, new CoreRuleEngine(), default, real.Events);
        Assert.True(replay.IsMatch, string.Join(";", replay.Errors));
        var item = Assert.Single(driver.State.StackItems, i => i.SourceObjectId == Source && i.PermanentPlayTargetsPending);
        var costs = item.OptionalCosts.ToList();
        if (mutation is "duplicate-haste" or "forged-paid") costs.Add(HasteOptionalCostNames.HasteReady);
        if (mutation == "wrong-haste") costs[0] = "HASTE_READY:orange";
        if (mutation == "unknown-haste") costs[0] = "HASTE_READY:cyan";
        if (mutation == "unrelated-fee") costs.Add("SPEND_MANA:42");
        if (mutation == "missing-fee") costs.RemoveAll(c => c.StartsWith("SPEND_POWER:", StringComparison.Ordinal));
        var forgedItem = item with { OptionalCosts = costs,
            SourceConfirmed = mutation == "unconfirmed" ? false : item.SourceConfirmed,
            PlayedFromHand = mutation == "forged-origin" || item.PlayedFromHand };
        var forgedState = driver.State with { StackItems = driver.State.StackItems.Select(i => i.StackItemId == item.StackItemId ? forgedItem : i).ToArray() };
        if (mutation == "forged-paid") forgedState = forgedState with {
            CardObjects = new Dictionary<string, CardObjectState>(forgedState.CardObjects) {
                [Source] = forgedState.CardObjects[Source] with { IsExhausted = false } } };
        var forged = Frame(initial, driver, forgedState);
        Assert.Equal(MatchStateHasher.HashValue(real.Commands), MatchStateHasher.HashValue(forged.Commands));
        Assert.Equal(MatchStateHasher.HashValue(real.Events), MatchStateHasher.HashValue(forged.Events));
        Assert.Equal(MatchStateHasher.Hash(real.ReplayInitialState!), MatchStateHasher.Hash(forged.ReplayInitialState!));
        // A syntactically valid claimed payment is proved by the unchanged raw
        // command replay, never by SourceConfirmed or a reconstructed aura alone.
        if (mutation == "forged-paid") Assert.Empty(forged.ValidationErrors);
        else Assert.NotEmpty(forged.ValidationErrors);
        var rejected = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, forged.Commands, forgedState, new CoreRuleEngine(), default, forged.Events);
        Assert.False(rejected.IsMatch); Assert.NotEmpty(rejected.Errors);
        Export("frame-" + card + "-" + mutation, new { scope = "explicit midgame frame and original-log audit; no ordinary-birth or registry claim",
            realFrame = real, forgedFrame = forged, positiveReplay = replay, forgedReplay = rejected });
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        await driver.Top();
        Assert.Equal(!haste, driver.State.CardObjects[Source].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static MatchRecoveryFrame Frame(MatchState initial, StandbyDefendObserverTests.Driver driver, MatchState state)
    {
        var commands = driver.Commands(); var events = driver.Events();
        var sequence = commands[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence,
            snapshots[id], state.Tick, sequence, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
        return new(state.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator);
    }

    private static string Reduction(string source, string component, string mode)
        => "OPTIONAL_COST_REDUCTION:" + source + ":0:" + component + ":" + mode;

    private static JsonElement Requirement(MatchState state)
    {
        var play = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        return JsonSerializer.SerializeToElement(play.Metadata, Json).GetProperty("sourceRequirements").EnumerateArray()
            .Single(r => r.GetProperty("sourceObjectId").GetString() == Source).Clone();
    }

    private static async Task Open(StandbyDefendObserverTests.Driver driver, bool spell)
    {
        if (spell)
        {
            await driver.Act("P1", new PlayCardCommand("PREREQUISITE", "OGN·057/298", ["HOST"]));
            await driver.Top();
        }
        await AttackDeckPlayTests.Attack(driver); await driver.Top(); await AttackDeckPlayTests.Reveal(driver);
        await DeckPlaySequenceTests.Select(driver, Source);
        Assert.NotNull(driver.State.PendingEffectPlay);
    }

    private static string OwnFee(string card) => card switch {
        "SFD·098/221" => "SPEND_MANA:1", "OGN·044/298" => "SPEND_POWER:green:1",
        "UNL-028/219" => "SPEND_POWER:red:1", "UNL-122/219" => "SPEND_POWER:purple:1",
        _ => throw new ArgumentOutOfRangeException(nameof(card)) };

    private static async Task RetainRejectedOriginal(string branch, MatchState state, PlayCardCommand command,
        PlayCostQuoteDto quote, StandbyDefendObserverTests.Driver driver)
    {
        if (quote.CanPay) return;
        var journal = new Journal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, Json), Json)!;
        fields["promptId"] = JsonSerializer.SerializeToElement(session.PromptFor("P1").PromptId);
        fields["snapshotTick"] = JsonSerializer.SerializeToElement(state.Tick);
        var raw = JsonSerializer.SerializeToElement(fields);
        var result = await session.SubmitAsync("P1", "compound-original", GameCommandJsonMapper.Map(raw), raw, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Export(branch, new { quote, raw, result.Accepted, result.ErrorCode, result.ErrorMessage,
            commands = driver.Commands(), events = driver.Events(), rejected = journal.Entries,
            beforeHash = MatchStateHasher.Hash(state), afterHash = MatchStateHasher.Hash(result.State) });
    }

    private static void Export(string branch, object value)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_COMPOUND_COST_EVIDENCE");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, branch.Replace('·', '-').Replace('/', '-') + ".json"), JsonSerializer.Serialize(value, Json));
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
