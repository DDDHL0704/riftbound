using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState BorrowedAttachmentPosition(string donor = "SFD·193/221", bool attached = false)
    {
        var state = LocalDestructionRecallTests.At(BorrowedUnitTokenPosition(donor), "ARMAMENT", "P1", "BASE");
        state = HandDiscardObserverTests.Put(state, "ENEMY-GEAR", "SFD·022/221", "P2", "BASE");
        return state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Where(id => id != "ARMAMENT").ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ARMAMENT"] = state.CardObjects["ARMAMENT"] with { AttachedToObjectId = attached ? "ALLY" : null, OwnerId = "P2" } } };
    }

    private static string BorrowedAttachmentId(MatchState state, bool attached)
        => TemplateId(state, attached ? "一件已贴附" : "一件未贴附");

    [Theory]
    [InlineData("SFD·193/221", false)]
    [InlineData("SFD·245/221", false)]
    [InlineData("SFD·193/221", true)]
    [InlineData("SFD·245/221", true)]
    public async Task BorrowedArmamentSkillsSeparateBothTargetRolesPayAndMoveTheEquipment(string donor, bool attached)
    {
        var initial = BorrowedAttachmentPosition(donor, attached);
        var id = BorrowedAttachmentId(initial, attached);
        var requirement = ResolutionResult.BuildPrompts(initial)["P1"].Candidates!
            .Where(c => c.Action == CommandTypes.ActivateAbility)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Single(r => Equals(r["abilityId"], id));
        var choices = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(requirement["targetChoicesByIndex"]);
        Assert.Contains(choices["0"], c => c.Id == "HOST");
        Assert.DoesNotContain(choices["0"], c => c.Id is "ARMAMENT" or "ENEMY-GEAR");
        Assert.Contains(choices["1"], c => c.Id == "ARMAMENT");
        Assert.DoesNotContain(choices["1"], c => c.Id is "HOST" or "ALLY" or "ENEMY-GEAR");
        foreach (var invalid in new[] { Array.Empty<string>(), ["ARMAMENT", "HOST"], ["HOST", "HOST"],
            ["HOST", "DONOR"], ["HOST", "ENEMY-GEAR"], ["HOST", "MISSING"] })
            await RejectBorrowedResource(initial, "HOST", id, invalid);
        if (!attached)
            await RejectBorrowedResource(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = new(0, 20) } }, "HOST", id, ["HOST", "ARMAMENT"]);
        var wrongState = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ARMAMENT"] = initial.CardObjects["ARMAMENT"] with { AttachedToObjectId = attached ? null : "ALLY" } } };
        await RejectBorrowedResource(wrongState, "HOST", id, ["HOST", "ARMAMENT"]);
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["HOST", "ARMAMENT"]));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.CardObjects["DONOR"].IsExhausted);
        Assert.Equal(initial.RunePools["P1"].Mana - (attached ? 0 : 1), game.State.RunePools["P1"].Mana);
        Assert.Equal(attached ? "ALLY" : null, game.State.CardObjects["ARMAMENT"].AttachedToObjectId);
        var item = Assert.Single(game.State.StackItems);
        Assert.Null(item.LegendAttachment);
        Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [item with { BorrowedExhaust = item.BorrowedExhaust! with { AbilityIndex = 99 } }] }));
        await game.Top();
        Assert.Equal("HOST", game.State.CardObjects["ARMAMENT"].AttachedToObjectId);
        Assert.Equal("P2", game.State.CardObjects["ARMAMENT"].OwnerId);
        Assert.Equal("P1", game.State.CardObjects["ARMAMENT"].ControllerId);
        Assert.Equal("F", game.State.ObjectLocations["ARMAMENT"].BattlefieldObjectId);
        Assert.Contains("ARMAMENT", game.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(attached ? 1 : 0, game.Events().Count(e => e.Event.Kind == "EQUIPMENT_DETACHED"));
        Assert.Single(game.Events(), e => e.Event.Kind == "EQUIPMENT_ATTACHED");
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("HOST")]
    [InlineData("ALLY")]
    [InlineData("DETACH")]
    public async Task BorrowedArmamentSkillRechecksBothCapturedTargetsAfterRealResponses(string recalled)
    {
        var detaches = recalled == "DETACH";
        var initial = BorrowedAttachmentPosition(attached: detaches);
        if (detaches) initial = LocalDestructionRecallTests.At(initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ARMAMENT"] = initial.CardObjects["ARMAMENT"] with { AttachedToObjectId = "HOST" } } },
            "ARMAMENT", "P1", "BATTLEFIELD", "F");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", BorrowedAttachmentId(initial, detaches), ["ALLY", "ARMAMENT"]));
        var returned = detaches ? "HOST" : recalled;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned]));
        await game.Top();
        Assert.Contains(returned, game.State.PlayerZones["P1"].Hand);
        if (detaches) Assert.Null(game.State.CardObjects["ARMAMENT"].AttachedToObjectId);
        await game.Top();
        Assert.Equal(recalled == "HOST" ? "ALLY" : null, game.State.CardObjects["ARMAMENT"].AttachedToObjectId);
        Assert.Equal(recalled == "HOST" ? 1 : 0, game.Events().Count(e => e.Event.Kind == "EQUIPMENT_ATTACHED"));
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedArmamentReattachmentToItsCurrentHostIsLegalAndDoesNotRepeatAttachment()
    {
        var initial = BorrowedAttachmentPosition(attached: true);
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", BorrowedAttachmentId(initial, true), ["ALLY", "ARMAMENT"]));
        await game.Top();
        Assert.Equal("ALLY", game.State.CardObjects["ARMAMENT"].AttachedToObjectId);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "EQUIPMENT_ATTACHED" or "EQUIPMENT_DETACHED" or "TRIGGER_QUEUED");
        Assert.Equal(initial.RunePools["P1"].Mana, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedArmamentAttachmentUsesTheSharedObserveThenCopyTextOrder()
    {
        var initial = BorrowedAttachmentPosition();
        initial = HandDiscardObserverTests.Put(initial, "OBSERVER", "SFD·119/221", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", BorrowedAttachmentId(initial, false), ["OBSERVER", "GEAR"]));
        await game.Top();
        Assert.Equal("OBSERVER", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("SFD·119/221", game.State.CardObjects["GEAR"].AttachedRuleText!.CardNo);
        var observed = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Single(game.State.StackItems, i => i.AttachmentObserver is not null);
        var events = game.Events().Select(e => e.Event).ToList();
        Assert.True(events.FindIndex(e => e.Kind == "EQUIPMENT_ATTACHED") < events.FindIndex(e => e.Kind == "RULE_TEXT_COPIED"));
        await game.Act("P1", new PayCostCommand(observed.PaymentId, observed.PaymentWindow, ["DECLINE"]));
        Assert.Null(game.State.PendingPayment);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedArmamentSkillPaysButCannotAttachAnArmamentAnimatedByTheRealMainPhaseTrigger()
    {
        var initial = BorrowedAttachmentPosition();
        initial = AnimatedArmamentAttachmentTests.WithMainPhaseResources(initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ORI"] = OfficialGarenConquestBattlefieldCountTests.Printed("ORI", "RAD·145/167", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = [..initial.PlayerZones["P1"].LegendZone, "ORI"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["ORI"] = new("P1", "LEGEND") } });
        foreach (var player in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(0, 3).Select(i => player + "-DECK-" + i).ToArray();
            var cards = initial.CardObjects.ToDictionary();
            var locations = initial.ObjectLocations.ToDictionary();
            foreach (var card in deck)
            {
                cards[card] = OfficialGarenConquestBattlefieldCountTests.Printed(card, "OGN·001/298", player);
                locations[card] = new(player, "MAIN_DECK");
            }
            initial = initial with { CardObjects = cards, ObjectLocations = locations,
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                    [player] = initial.PlayerZones[player] with { MainDeck = [..initial.PlayerZones[player].MainDeck, ..deck] } } };
        }
        var game = new Driver(initial);
        await AnimatedArmamentAttachmentTests.TransformAtNextOwnMain(game, "ARMAMENT");
        Assert.Contains(CardObjectTags.UnitCard, game.State.CardObjects["ARMAMENT"].Tags);
        var location = game.State.ObjectLocations["ARMAMENT"];
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new ActivateAbilityCommand("HOST", BorrowedAttachmentId(game.State, false), ["HOST", "ARMAMENT"]));
        Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
        await game.Top();
        Assert.Null(game.State.CardObjects["ARMAMENT"].AttachedToObjectId);
        Assert.Equal(location, game.State.ObjectLocations["ARMAMENT"]);
        Assert.NotNull(game.State.CardObjects["ARMAMENT"].LegendEquipmentUnit);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "EQUIPMENT_ATTACHED");
        await game.Replay();
    }
}
