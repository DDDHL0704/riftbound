using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.OfficialTokenReplacementTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialLegendTokenTests
{
    [Fact]
    public async Task SecondPlayerCanActivateAndChooseItsOwnReplacement()
    {
        var s = Position("OGN·265/298");
        s = s with { ActivePlayerId = "P2", TurnPlayerId = "P2",
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { LegendZone = [], Battlefields = [] },
                ["P2"] = s.PlayerZones["P2"] with { LegendZone = ["LEGEND"], Battlefields = ["BFZ", "Z1"] } },
            CardObjects = s.CardObjects.ToDictionary(e => e.Key, e => new[] { "LEGEND", "BFZ", "Z1" }.Contains(e.Key)
                ? e.Value with { OwnerId = "P2", ControllerId = "P2" } : e.Value),
            ObjectLocations = s.ObjectLocations.ToDictionary(e => e.Key, e => new[] { "LEGEND", "BFZ", "Z1" }.Contains(e.Key)
                ? e.Value with { PlayerId = "P2" } : e.Value) };
        var activated = await Act(s, "P2", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.ViktorLegendAbilityId, [], ["SPEND_MANA:1"]));
        var pending = await Top(activated.State);
        Assert.Equal("P2", pending.State.PendingCardChoice!.PlayerId); Restore(pending.State);
        var done = await Choose(pending.State, "Z1");
        Assert.Equal(2, Tokens(done.State).Length);
        Assert.All(Tokens(done.State), t => { Assert.Equal("P2", t.ControllerId); Assert.Contains(t.ObjectId, done.State.PlayerZones["P2"].Base); });
        Assert.Equal(29, done.State.RunePools["P2"].Mana); Assert.Equal(30, done.State.RunePools["P1"].Mana); Restore(done.State);
    }

    [Theory]
    [InlineData("unit", 3)]
    [InlineData("equipment", 4)]
    [InlineData("hidden", 4)]
    [InlineData("enemy", 4)]
    public async Task LilliaReductionAndPromptCountOnlyVisibleFriendlyEphemeralUnits(string kind, int mana)
    {
        var s = Position("UNL-189/219", 0);
        s = s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                [kind == "enemy" ? "P2" : "P1"] = s.PlayerZones[kind == "enemy" ? "P2" : "P1"] with { Base = ["SUPPORT"] } },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["SUPPORT"] = new("SUPPORT", cardNo: kind == "equipment" ? "SFD·095/221" : "UNL·T07",
                    ownerId: kind == "enemy" ? "P2" : "P1", controllerId: kind == "enemy" ? "P2" : "P1", power: 3,
                    isFaceDown: kind == "hidden", tags: [kind == "equipment" ? CardObjectTags.EquipmentCard : CardObjectTags.UnitCard, CardObjectTags.Ephemeral]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["SUPPORT"] = new(kind == "enemy" ? "P2" : "P1", "BASE") } };
        var prompt = new MatchSession(s, new CoreRuleEngine(), NoopMatchJournal.Instance).PromptFor("P1");
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.LegendAct);
        Assert.Contains($"SPEND_MANA:{mana}", System.Text.Json.JsonSerializer.Serialize(candidate));
        var activated = await Act(s, "P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.LilliaLegendAbilityId, [], [$"SPEND_MANA:{mana}"]));
        Assert.Equal(30 - mana, activated.State.RunePools["P1"].Mana);
        // A later board change cannot retroactively reprice a paid activation.
        var changed = activated.State with { CardObjects = new Dictionary<string, CardObjectState>(activated.State.CardObjects) {
            ["SUPPORT"] = activated.State.CardObjects["SUPPORT"] with { Tags = [CardObjectTags.UnitCard] } } };
        var done = await Top(changed);
        Assert.Equal(30 - mana, done.State.RunePools["P1"].Mana); Assert.Single(Tokens(done.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EachReplacementTokenInheritsReadyAdjustmentAndTriggersEnemyEntry(bool hiddenRenata)
    {
        var s = Position("SFD·197/221");
        s = s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Base = ["R"] },
                ["P2"] = s.PlayerZones["P2"] with { Battlefields = ["BFV", "V"] } },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["R"] = new("R", cardNo: "SFD·171/221", power: 3, ownerId: "P1", controllerId: "P1", isFaceDown: hiddenRenata, tags: [CardObjectTags.UnitCard]),
                ["V"] = new("V", cardNo: "UNL-150/219", power: 4, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]),
                ["BFV"] = new("BFV", cardNo: "OGN·296/298", ownerId: "P2", controllerId: "P2", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) {
                ["R"] = new("P1", "BASE"), ["V"] = new("P2", "BATTLEFIELD", "BFV"), ["BFV"] = new("P2", "BATTLEFIELD", "BFV") } };
        var pending = await Top((await Activate(s, "SFD·197/221")).State);
        Assert.Empty(pending.State.TriggerQueue);
        var done = await Choose(pending.State, "Z1");
        Assert.Equal(2, Tokens(done.State).Length);
        Assert.All(Tokens(done.State), t => Assert.Equal(hiddenRenata, t.IsExhausted));
        Assert.Equal(2, done.State.TriggerQueue.Count); Assert.All(done.State.TriggerQueue, t => Assert.NotNull(t.UnitEntryContext)); Restore(done.State);
        done = await Act(done.State, "P2", new OrderTriggersCommand(OrderedTriggerIds: done.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        done = await Top(done.State); done = await Top(done.State);
        Assert.All(Tokens(done.State), t => Assert.Contains("STUNNED", t.UntilEndOfTurnEffects)); Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·265/298", 1, true)]
    [InlineData("SFD·197/221", 2, true)]
    [InlineData("UNL-189/219", 3, false)]
    public async Task ActivationPaysOnceThenRespondsBeforeReplacementAndEntry(string card, int power, bool exhausted)
    {
        var initial = Position(card, 2);
        var activated = await Activate(initial, card);
        Assert.Empty(Tokens(activated.State)); Assert.Null(activated.State.PendingCardChoice);
        Assert.Single(activated.State.StackItems); Assert.True(activated.State.CardObjects["LEGEND"].IsExhausted);
        Assert.All(activated.Snapshots.Values, snapshot => Assert.EndsWith("技能",
            Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack))["abilityLabel"] as string));
        var expectedMana = 30 - (card == "UNL-189/219" ? 4 : 1);
        Assert.Equal(expectedMana, activated.State.RunePools["P1"].Mana); Restore(activated.State);
        var passed = await Act(activated.State, "P1", new PassPriorityCommand());
        Assert.Equal("P2", passed.State.PriorityPlayerId); Assert.Empty(Tokens(passed.State));
        var pending = await Act(passed.State, "P2", new PassPriorityCommand());
        Assert.Equal("TOKEN_ENTRY_REPLACEMENT", pending.State.PendingCardChoice!.ChoiceWindow);
        Assert.Empty(Tokens(pending.State)); Restore(pending.State);
        pending = await Choose(pending.State, "Z2"); Restore(pending.State);
        var done = await Choose(pending.State, "Z1"); Restore(done.State);
        Assert.Equal(expectedMana, done.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(done.Events, e => e.Kind == "COST_PAID");
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        Assert.Equal(3, Tokens(done.State).Length);
        Assert.Equal(3, done.Events.Count(e => e.Kind == "UNIT_TOKEN_CREATED"));
        Assert.All(Tokens(done.State), token => {
            Assert.Equal(power, token.Power); Assert.Equal(exhausted, token.IsExhausted);
            Assert.Contains(CardObjectTags.UnitCard, token.Tags);
            Assert.Equal("BASE", done.State.ObjectLocations[token.ObjectId].Zone);
            if (card == "UNL-189/219") { Assert.Contains(CardObjectTags.Ephemeral, token.Tags); Assert.Contains("仙灵", token.Tags); }
            if (card == "SFD·197/221") Assert.Contains(CardEquipmentKeywordNames.Tempered, token.Tags);
        });
        Assert.Contains("LEGEND", done.State.PlayerZones["P1"].LegendZone);
    }

    [Theory]
    [InlineData("OGN·265/298")]
    [InlineData("SFD·197/221")]
    [InlineData("UNL-189/219")]
    public async Task DecliningReplacementKeepsUseAndDoesNotCancelPaidAbility(string card)
    {
        var pending = await Top((await Activate(Position(card), card)).State);
        var done = await Choose(pending.State);
        Assert.Single(Tokens(done.State));
        Assert.DoesNotContain("TOKEN_ENTRY_REPLACEMENT_USED", done.State.CardObjects["Z1"].UntilEndOfTurnEffects);
        Assert.True(done.State.CardObjects["LEGEND"].IsExhausted); Restore(done.State);
    }

    [Fact]
    public async Task ReplacementEligibilityIsEvaluatedAtResolutionAfterOpponentResponse()
    {
        var initial = Position("OGN·265/298");
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Hand = ["RESPONSE"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["RESPONSE"] = new("RESPONSE", cardNo: "UNL-128/219", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["RESPONSE"] = new("P2", "HAND") } };
        var activated = await Activate(initial, "OGN·265/298");
        var passed = await Act(activated.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("RESPONSE", "UNL-128/219", ["TARGET", "Z1"]));
        Assert.Equal(2, response.State.StackItems.Count);
        var returned = await Top(response.State);
        Assert.Contains("Z1", returned.State.PlayerZones["P1"].Hand);
        Assert.Empty(Tokens(returned.State)); Restore(returned.State);
        var s = returned.State;
        var done = await Top(s);
        Assert.Null(done.State.PendingCardChoice); Assert.Single(Tokens(done.State)); Restore(done.State);
    }

    [Fact]
    public async Task RecoveryRejectsMismatchedAbilityIdentityOrInventedRepeat()
    {
        var s = (await Activate(Position("OGN·265/298"), "OGN·265/298")).State;
        var item = s.StackItems.Single();
        foreach (var invalid in new[] { item with { CardNo = "UNL-199/219" }, item with { EffectRepeatCount = 2 }, item with { TargetObjectIds = ["Z1"] } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(s with { StackItems = [invalid] }), e => e.Contains("legend unit token"));
    }

    internal static MatchState Position(string card, int replacements = 1)
    {
        var s = OfficialTokenReplacementTests.Position(replacements);
        return s with { UntilEndOfTurnEffects = ["PLAYED_ARMAMENT_THIS_TURN:P1"],
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["LEGEND"] = new("LEGEND", cardNo: card, tags: ["CARD_TYPE:LEGEND"], ownerId: "P1", controllerId: "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
    }

    internal static Task<ResolutionResult> Activate(MatchState s, string card) => Act(s, "P1", new LegendActCommand("LEGEND",
        card == "OGN·265/298" ? LegendActionAbilityCatalog.ViktorLegendAbilityId
            : card == "SFD·197/221" ? LegendActionAbilityCatalog.AzirLegendAbilityId : LegendActionAbilityCatalog.LilliaLegendAbilityId,
        [], [card == "UNL-189/219" ? "SPEND_MANA:4" : "SPEND_MANA:1"]));
}
