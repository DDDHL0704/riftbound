using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OptionalEntryTargetConfirmationTests
{
    [Theory]
    [InlineData("SFD·013/221", 33085, 2, 2, 1, "red")]
    [InlineData("SFD·067/221", 33147, 3, 3, 0, "blue")]
    [InlineData("VEN·120", 37820, 4, 4, 0, "yellow")]
    public async Task ExactCurrentPrintingSeparatesItsBaseAndOptionalAdditionalCosts(string no, long id, int mana, int power, int extraMana, string trait)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(power, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(mana, behavior.ManaCost); Assert.Equal(power, behavior.SourceUnitPower);
        Assert.Equal(extraMana, behavior.TargetEffectAdditionalManaCost); Assert.Equal(1, behavior.TargetEffectAdditionalPowerCost);
        Assert.Equal(trait, behavior.TargetEffectAdditionalPowerTrait); Assert.Equal(0, PrintedPowerCostRules.ForCard(no).Amount);
        Assert.Equal(no switch {
            "SFD·013/221" => "你可以选择支付{{1}}和{{红色}}，作为打出我的额外费用。\n当你打出我时，如果你支付了该额外费用，则对战场上的一名单位造成2点伤害。",
            "SFD·067/221" => "你可以选择支付{{蓝色}}，作为打出我的额外费用。\n当你打出我时，如果你支付了该额外费用，则让一名单位本回合内{{S}}-2。",
            _ => "你可以选择支付{{黄色}}，作为打出我的额外费用。 \n当你打出我时，如果你支付了该额外费用，则{{眩晕}}战场上的一名敌方单位。（使其在本回合内无法造成战斗伤害。）" }, card.CardEffect);
    }

    [Theory]
    [InlineData("SFD·013/221", 17, 2)]
    [InlineData("SFD·067/221", 17, 0)]
    [InlineData("VEN·120", 16, 0)]
    public async Task PayingThePrintedAdditionalCostDoesNotMoveTheIndependentTriggerTargetChoiceBeforeEntry(
        string no, int remainingMana, int damage)
    {
        var initial = Position(no); var driver = new Driver(initial);
        var costs = Costs(no);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: costs, Destination: "BASE"));
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        Assert.Equal(remainingMana, driver.State.RunePools["P1"].Mana);
        Assert.Equal(5, driver.State.RunePools["P1"].TotalPower);
        var trait = no == "SFD·013/221" ? RuneTrait.Red : no == "SFD·067/221" ? RuneTrait.Blue : RuneTrait.Yellow;
        foreach (var color in new[] { RuneTrait.Red, RuneTrait.Blue, RuneTrait.Yellow })
            Assert.Equal(color == trait ? 1 : 2, driver.State.RunePools["P1"].PowerByTrait[color]);
        Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(4, driver.State.CardObjects["ENEMY"].Power);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow); Assert.Equal("UNIT", choice.SourceObjectId);
        Assert.Equal(1, choice.RequiredCount); Assert.Contains("ENEMY", choice.LegalObjectIds);
        if (no == "SFD·067/221") Assert.Contains("UNIT", choice.LegalObjectIds);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await driver.Top(); Assert.Empty(driver.State.StackItems);
        Assert.Equal(damage, driver.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(no == "SFD·067/221" ? 2 : 4, driver.State.CardObjects["ENEMY"].Power);
        Assert.Equal(no == "VEN·120", driver.State.CardObjects["ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
        await driver.Replay();
    }

    [Theory]
    [InlineData("SFD·013/221", 18)]
    [InlineData("SFD·067/221", 17)]
    [InlineData("VEN·120", 16)]
    public async Task DecliningThePrintedAdditionalCostEntersTheUnitWithoutCreatingTheConditionalSkill(string no, int remainingMana)
    {
        var driver = new Driver(Position(no));
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        Assert.Equal(remainingMana, driver.State.RunePools["P1"].Mana); Assert.Equal(6, driver.State.RunePools["P1"].TotalPower);
        Assert.Empty(driver.State.StackItems); Assert.Null(driver.State.PendingCardChoice);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "TRIGGER_QUEUED");
        await OptionalEntryTargetBoundaryTests.Reject(driver.State, "P1", new PassPriorityCommand());
        await OptionalEntryTargetBoundaryTests.Reject(driver.State, "P2", new PassPriorityCommand());
        await driver.Replay();
    }

    [Fact]
    public async Task TheCubsConfirmedReductionKeepsItsPublicHistoricalSourceAfterThatSourceReturnsToHand()
    {
        var initial = OptionalEntryTargetConfirmationTests.AddSpell(Position("SFD·067/221"), "RETURN", "OGN·104/298");
        var driver = new Driver(initial);
        // A source command can carry a target-selection hint. The new shared
        // path must still check it only against the post-entry field.
        await driver.Act("P1", new PlayCardCommand("UNIT", "SFD·067/221", ["ALLY"], OptionalCosts: Costs("SFD·067/221"), Destination: "BASE"));
        if (driver.State.PendingCardChoice is { } choice)
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand); Assert.Single(driver.State.StackItems);
        await driver.Top(); Assert.Equal(2, driver.State.CardObjects["ALLY"].Power); await driver.Replay();
    }

    internal static MatchState AddSpell(MatchState state, string id, string no)
        => OfficialCopyIdentityTests.AddSpell(state, id, no) with { RunePools = state.RunePools };

    [Fact]
    public async Task ADeclaredCubSelfTargetIsBoundOnlyAfterTheUnitActuallyEnters()
    {
        var initial = Position("SFD·067/221"); var driver = new Driver(initial);
        // A pre-entry hint must already name a legal public target. The
        // source becomes available through its later independent choice.
        await OptionalEntryTargetBoundaryTests.Reject(initial, "P1", new PlayCardCommand("UNIT", "SFD·067/221", ["UNIT"], OptionalCosts: Costs("SFD·067/221"), Destination: "BASE"));
        await driver.Act("P1", new PlayCardCommand("UNIT", "SFD·067/221", [], OptionalCosts: Costs("SFD·067/221"), Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!; Assert.Contains("UNIT", choice.LegalObjectIds);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
        Assert.Null(driver.State.PendingCardChoice);
        var skill = Assert.Single(driver.State.StackItems);
        Assert.Equal(["UNIT"], skill.TargetObjectIds);
        Assert.Equal(driver.State.CardObjects["UNIT"].ObjectGeneration, skill.TargetGenerations!["UNIT"]);
        Assert.Equal(3, driver.State.CardObjects["UNIT"].Power);
        var kinds = driver.Events().Select(e => e.Event.Kind).ToArray();
        Assert.True(Array.IndexOf(kinds, "UNIT_PLAYED_TO_BASE") < Array.IndexOf(kinds, "TRIGGER_CONFIRMED"));
        await driver.Top(); Assert.Equal(1, driver.State.CardObjects["UNIT"].Power);
        await driver.Act("P1", new EndTurnCommand()); Assert.Equal(3, driver.State.CardObjects["UNIT"].Power); await driver.Replay();
    }

    [Fact]
    public async Task PayingMarzasYellowCostWithNoEnemyBattlefieldUnitStillEntersWithoutAStunTarget()
    {
        var initial = Position("VEN·120");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·120", [], OptionalCosts: Costs("VEN·120"), Destination: "BATTLEFIELD:BF"));
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Battlefields);
        Assert.Contains("约德尔人", driver.State.CardObjects["UNIT"].Tags);
        Assert.True(driver.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(16, driver.State.RunePools["P1"].Mana); Assert.Equal(1, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
        Assert.Null(driver.State.PendingCardChoice); Assert.Empty(driver.State.StackItems);
        Assert.DoesNotContain("STUNNED", driver.State.CardObjects["ENEMY"].UntilEndOfTurnEffects); await driver.Replay();
    }

    internal static string[] Costs(string no) => no switch {
        "SFD·013/221" => ["SPEND_MANA:1", "SPEND_POWER:red:1"],
        "SFD·067/221" => ["SPEND_POWER:blue:1"], _ => ["SPEND_POWER:yellow:1"] };

    [Fact]
    public async Task NoBattlefieldTargetDoesNotPreventAnOtherwiseLegalPaidPermanentEntryOrRefundItsCost()
    {
        var initial = Position("SFD·013/221");
        initial = initial with { PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P2"
                ? p.Value with { Base = ["ENEMY"], Battlefields = ["OTHER"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["ENEMY"] = new("P2", "BASE") } };
        var driver = new Driver(initial);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var play = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(play.Metadata!["sourceRequirements"]);
        var requirement = Assert.Single(requirements, r => Equals(r["sourceObjectId"], "UNIT"));
        var optional = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]);
        Assert.Contains(optional, c => c.Id == "SPEND_MANA:1");
        Assert.Contains(optional, c => c.Id == "SPEND_POWER:red:1");
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial,
            new("UNIT", "SFD·013/221", [], OptionalCosts: Costs("SFD·013/221"), Destination: "BASE")));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana);
        await driver.Act("P1", new PlayCardCommand("UNIT", "SFD·013/221", [],
            OptionalCosts: ["SPEND_MANA:1", "SPEND_POWER:red:1"], Destination: "BASE"));
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        Assert.Equal(17, driver.State.RunePools["P1"].Mana); Assert.Equal(5, driver.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain("BF", driver.State.PendingCardChoice?.LegalObjectIds ?? []);
        Assert.DoesNotContain("OTHER", driver.State.PendingCardChoice?.LegalObjectIds ?? []);
        Assert.Empty(driver.State.StackItems); Assert.Null(driver.State.PendingCardChoice);
        await driver.Replay();
    }

    internal static MatchState Position(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        return state with { RoomId = "OPTIONAL-ENTRY-TARGETS",
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P2"
                ? p.Value with { Base = [], Battlefields = ["OTHER", "ENEMY"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["ENEMY"] = new("P2", "BATTLEFIELD", "OTHER") },
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 0, new Dictionary<string, int> {
                [RuneTrait.Red] = 2, [RuneTrait.Blue] = 2, [RuneTrait.Yellow] = 2 }), ["P2"] = new(20, 20) } };
    }
}
