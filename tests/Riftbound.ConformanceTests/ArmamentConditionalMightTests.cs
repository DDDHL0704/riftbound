using Riftbound.Contracts;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGarenConquestBattlefieldCountTests;

namespace Riftbound.ConformanceTests;

public sealed class ArmamentConditionalMightTests
{
    [Theory]
    [InlineData("SFD·042/221", 2, "ATTACHED_THIS_TURN", 0)]
    [InlineData("UNL-039/219", 1, "CONTROLLER_EXPERIENCE_AT_LEAST", 3)]
    [InlineData("VEN·027", 2, "EXACT_OTHER_FRIENDLY_UNITS_HERE", 1)]
    public void OfficialLowerBoxProvidesTheExactConditionAmountAndThreshold(string cardNo, int amount,
        string condition, int threshold)
        => Assert.Equal(new OfficialArmamentConditionalMight(amount, condition, threshold),
            OfficialArmamentCatalog.Find(cardNo)!.ConditionalMight);

    [Fact]
    public void OnlyTheThreeMatchingOfficialLowerBoxesAcquireAConditionalMightDefinition()
        => Assert.Equal(new[] { "SFD·042/221", "UNL-039/219", "VEN·027" }, OfficialArmamentCatalog.All
            .Where(definition => definition.ConditionalMight is not null).Select(definition => definition.CardNo)
            .Order(StringComparer.Ordinal).ToArray());

    private static MatchState Position(string gear, int experience = 0, string host = "OGN·176/298")
    {
        var state = VenUnitEntryBasicTests.Position(gear);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ALLY"] = Printed("ALLY", host, "P1") },
            PlayerExperience = new Dictionary<string, int>(state.PlayerExperience) { ["P1"] = experience },
            RunePools = state.RunePools.ToDictionary(p => p.Key, _ => new RunePool(40, 10,
                new Dictionary<string, int> { ["red"] = 10, ["green"] = 10, ["blue"] = 10,
                    ["orange"] = 10, ["purple"] = 10, ["yellow"] = 10 })) };
    }

    private static int Might(Driver game) => ThreeBattlefieldTimingProbe.EffectivePower(game.State, "P1", "ALLY");
    private static async Task Attach(Driver game, string no)
    {
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(2, Might(game));
        await game.Act("P1", new AssembleEquipmentCommand("UNIT", "ALLY", ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.Equal("ALLY", game.State.CardObjects["UNIT"].AttachedToObjectId);
    }

    [Theory]
    [InlineData("SFD·042/221", 5)]
    [InlineData("UNL-039/219", 4)]
    [InlineData("VEN·027", 3)]
    public async Task ActualPlayAndAttachmentApplyOnlyTheMatchingConditionalBonusAndDetachRemovesIt(string no, int expected)
    {
        var state = AttackDamageDistributionTests.Spell(Position(no, 3), "DETACH", "SFD·011/221", "P1");
        var game = new Driver(state); await Attach(game, no); Assert.Equal(expected, Might(game));
        Assert.Equal(2, game.State.CardObjects["ALLY"].Power);
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["ALLY", "UNIT"])); await game.Top();
        Assert.Null(game.State.CardObjects["UNIT"].AttachedToObjectId); Assert.Equal(2, Might(game)); await game.Replay();
    }

    [Theory]
    [InlineData(0, 3)] [InlineData(2, 3)] [InlineData(3, 4)] [InlineData(4, 4)]
    public async Task LevelThreeUsesTheHostsActualExperience(int experience, int expected)
    {
        var game = new Driver(Position("UNL-039/219", experience)); await Attach(game, "UNL-039/219");
        Assert.Equal(experience, game.State.PlayerExperience["P1"]); Assert.Equal(expected, Might(game)); await game.Replay();
    }

    [Fact]
    public async Task AttachmentTurnBonusExpiresAtTheRealEndOfTurn()
    {
        var game = new Driver(Position("SFD·042/221")); await Attach(game, "SFD·042/221");
        Assert.Equal(5, Might(game)); await game.Act("P1", new EndTurnCommand());
        Assert.Equal(3, Might(game)); Assert.Equal("ALLY", game.State.CardObjects["UNIT"].AttachedToObjectId); await game.Replay();
    }

    [Fact]
    public async Task MechFanDoublesTheBaseBonusAndDoesNotDoubleTheConditionalTwo()
    {
        var game = new Driver(Position("SFD·042/221", host: "SFD·068/221"));
        await game.Act("P1", new PlayCardCommand("UNIT", "SFD·042/221", []));
        Assert.Equal(3, Might(game));
        await game.Act("P1", new AssembleEquipmentCommand("UNIT", "ALLY", ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.Equal(7, Might(game)); Assert.Equal(3, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    [Fact]
    public async Task GavelTracksExactlyOneOtherFriendlyUnitAtItsActualBattlefield()
    {
        var state = Position("VEN·027");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["FRIEND"] = Printed("FRIEND", "OGN·176/298", "P1"), ["SECOND"] = Printed("SECOND", "OGN·176/298", "P1") } };
        state = LocalDestructionRecallTests.At(LocalDestructionRecallTests.At(state, "FRIEND", "P1", "BASE"), "SECOND", "P1", "BASE");
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state); await Attach(game, "VEN·027"); Assert.Equal(3, Might(game));
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF"));
        await CloseFocus(game); Assert.Equal(3, Might(game));
        await game.Act("P1", new MoveUnitCommand("FRIEND", "BASE", "BATTLEFIELD:BF"));
        await CloseFocus(game); Assert.Equal(5, Might(game));
        await game.Act("P1", new MoveUnitCommand("SECOND", "BASE", "BATTLEFIELD:BF"));
        await CloseFocus(game); Assert.Equal(3, Might(game));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SECOND"])); await game.Top();
        Assert.Equal(5, Might(game)); Assert.Contains("SECOND", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task SpendingRealExperienceImmediatelyDisablesTheLevelBonus()
    {
        var state = MightyExperienceFrameTests.HandUnit(Position("UNL-039/219", 3), "POPPY", "UNL-178/219");
        var game = new Driver(state); await Attach(game, "UNL-039/219"); Assert.Equal(4, Might(game));
        await game.Act("P1", new PlayCardCommand("POPPY", "UNL-178/219", [], OptionalCosts: ["SPEND_EXPERIENCE:3"]));
        Assert.Equal(0, game.State.PlayerExperience["P1"]); Assert.Equal(3, Might(game)); await game.Replay();
    }

    [Fact]
    public async Task RealExperienceGainActivatesTheLevelBonusAfterTheBaseBonusCrossesMighty()
    {
        var state = MightyExperienceFrameTests.HandUnit(Position("UNL-039/219", 2, "OGN·219/298"), "ENVOY", "UNL-092/219");
        var game = new Driver(state); Assert.Equal(4, Might(game));
        await game.Act("P1", new PlayCardCommand("UNIT", "UNL-039/219", []));
        await game.Act("P1", new AssembleEquipmentCommand("UNIT", "ALLY", ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.Equal(2, game.State.PlayerExperience["P1"]); Assert.Equal(5, Might(game));
        await game.Act("P1", new PlayCardCommand("ENVOY", "UNL-092/219", [])); await game.Top();
        Assert.Equal(3, game.State.PlayerExperience["P1"]); Assert.Equal(6, Might(game));
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    [Theory]
    [InlineData(3, 0, 3)]
    [InlineData(0, 3, 4)]
    public async Task ARealControlTransferUsesTheCurrentControllersExperienceInsteadOfItsOwners(
        int ownerExperience, int newControllerExperience, int expected)
    {
        var initial = RadGearTestPosition.Put(Position("UNL-039/219", ownerExperience),
            "TAKE", "OGN·203/298", "P2", "HAND");
        initial = initial with { PlayerExperience = new Dictionary<string, int>(initial.PlayerExperience) {
            ["P2"] = newControllerExperience } };
        for (var i = 0; i < 8; i++)
            initial = RadGearTestPosition.Put(initial, "TAKE-RUNE-" + i, "UNL-R05", "P2", "BASE");
        var game = new Driver(initial); await Attach(game, "UNL-039/219");
        Assert.Equal(ownerExperience >= 3 ? 4 : 3, Might(game));
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF"));
        await CloseFocus(game);
        await game.Act("P1", new EndTurnCommand());
        await game.TapRunes("P2", Enumerable.Range(0, 8).Select(i => "TAKE-RUNE-" + i));
        await game.Act("P2", new RecycleRuneCommand("TAKE-RUNE-0", ["TAKE-RUNE-0", "TAKE-RUNE-1", "TAKE-RUNE-2"]));
        await game.Act("P2", new PlayCardCommand("TAKE", "OGN·203/298", ["ALLY"]));
        await game.Top();
        Assert.Equal("P1", game.State.CardObjects["ALLY"].OwnerId);
        Assert.Equal("P2", game.State.CardObjects["ALLY"].ControllerId);
        Assert.Equal("ALLY", game.State.CardObjects["UNIT"].AttachedToObjectId);
        Assert.Contains("ALLY", game.State.PlayerZones["P2"].Base);
        Assert.Equal(expected, ThreeBattlefieldTimingProbe.EffectivePower(game.State, "P2", "ALLY"));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task TheRealAgileAttachmentEarnedFromPlayedJaxAlsoStartsTheConditionalTurnBonus()
    {
        var initial = RadGearTestPosition.Put(Position("SFD·042/221"), "JAX", "SFD·054/221", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("JAX", "SFD·054/221", []));
        if (game.State.StackItems.Count > 0) await game.Top();
        await game.Act("P1", new PlayCardCommand("UNIT", "SFD·042/221", []));
        Assert.Equal(2, Might(game));
        Assert.Equal("AGILE_PLAY", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await game.Top();
        Assert.Equal("ALLY", game.State.CardObjects["UNIT"].AttachedToObjectId);
        Assert.Equal(5, Might(game));
        Assert.Single(game.Events(), entry => entry.Event.Kind == "EQUIPMENT_ATTACHED");
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task GavelCountsTheActuallyCreatedFriendlyUnitTokenAndReevaluatesEachRealDeparture()
    {
        var initial = RadGearTestPosition.Put(Position("VEN·027"), "GUARD", "SFD·157/221", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "RETURN-GUARD", "OGN·104/298", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "RETURN-TOKEN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial); await Attach(game, "VEN·027");
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF"));
        await CloseFocus(game); Assert.Equal(3, Might(game));
        await game.Act("P1", new PlayCardCommand("GUARD", "SFD·157/221", [], Destination: "BATTLEFIELD:BF"));
        for (var guard = 0; game.State.StackItems.Count > 0; guard++)
        {
            Assert.True(guard < 4); await game.Top();
        }
        var token = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo is not null);
        Assert.Contains(CardObjectTags.UnitCard, token.Tags);
        Assert.Equal("P1", token.ControllerId);
        Assert.Equal("BF", game.State.ObjectLocations["GUARD"].BattlefieldObjectId);
        Assert.Equal("BF", game.State.ObjectLocations[token.ObjectId].BattlefieldObjectId);
        Assert.Equal(3, Might(game)); // Royal Guard plus its real unit token: two others.
        await game.Act("P1", new PlayCardCommand("RETURN-GUARD", "OGN·104/298", ["GUARD"]));
        await game.Top(); Assert.Equal(5, Might(game));
        Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Battlefields);
        await game.Act("P1", new PlayCardCommand("RETURN-TOKEN", "OGN·104/298", [token.ObjectId]));
        await game.Top(); Assert.Equal(3, Might(game));
        Assert.False(game.State.CardObjects.ContainsKey(token.ObjectId));
        await game.ColdCheckpoint();
    }

    // Compare forged state against its original accepted attachment journal;
    // original commands and events remain unchanged.
    // If the candidate accepts a forged marker, add genuine attachment history
    // binding and rerun these same counterexamples before publishing it.
    [Theory]
    [InlineData("erase-live")]
    [InlineData("add-before-attachment")]
    [InlineData("resurrect-after-end")]
    [InlineData("transfer-to-another-equipment")]
    public async Task AttachmentTurnStateMustMatchItsOriginalAcceptedAttachmentHistory(string mutation)
    {
        const string marker = "ARMAMENT_ATTACHED_THIS_TURN";
        const string cardNo = "SFD·042/221";
        var initial = RadGearTestPosition.Put(Position(cardNo), "OTHER-GEAR", cardNo, "P1", "BASE");
        var game = new Driver(initial);
        if (mutation == "add-before-attachment")
            await game.Act("P1", new PlayCardCommand("UNIT", cardNo, []));
        else
            await Attach(game, cardNo);
        if (mutation == "resurrect-after-end")
            await game.Act("P1", new EndTurnCommand());
        var original = MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() });
        var cards = new Dictionary<string, CardObjectState>(game.State.CardObjects);
        var live = mutation is "erase-live" or "transfer-to-another-equipment";
        Assert.Equal(live, cards["UNIT"].UntilEndOfTurnEffects.Contains(marker));
        cards["UNIT"] = cards["UNIT"] with { UntilEndOfTurnEffects = live
            ? cards["UNIT"].UntilEndOfTurnEffects.Where(effect => effect != marker).ToArray()
            : cards["UNIT"].UntilEndOfTurnEffects.Append(marker).ToArray() };
        if (mutation == "transfer-to-another-equipment")
            cards["OTHER-GEAR"] = cards["OTHER-GEAR"] with {
                UntilEndOfTurnEffects = cards["OTHER-GEAR"].UntilEndOfTurnEffects.Append(marker).ToArray() };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { CardObjects = cards }));
        Assert.Equal(original, MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() }));
        await game.ColdCheckpoint();
    }

    private static async Task CloseFocus(Driver game)
    {
        for (var guard = 0; game.State.FocusPlayerId is { } focus; guard++)
        {
            Assert.True(guard < 4); await game.Act(focus, new PassFocusCommand());
        }
    }
}
