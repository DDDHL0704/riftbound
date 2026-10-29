using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class JayceEquipmentPlayPermissionTests
{
    private const string PermissionPrefix = "EQUIPMENT_PLAY_PERMISSION:";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static MatchState Position(string no = "VEN·175", int mana = 20, int yellow = 2)
    {
        var s = EmpowerTests.Position(no);
        s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) {
            ["P1"] = new(mana, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = yellow, [RuneTrait.Blue] = 2 }) } };
        foreach (var (id, card) in new[] { ("SEVEN", "SFD·168/221"), ("SECOND", "SFD·168/221"),
            ("EXPENSIVE", "OGN·160/298"), ("UNIT", "OGN·001/298") })
            s = AddHand(s, id, card);
        return s;
    }

    private static MatchState AddHand(MatchState state, string id, string no) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1") },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", "HAND") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = [..state.PlayerZones["P1"].Hand, id] } } };

    private static MatchState GoldPosition()
    {
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, out var factory));
        var initial = Position();
        return initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = factory.CreateObject("GEAR", "P1", "P1") with { IsExhausted = false } } };
    }

    private static async Task Choose(Driver game, params string[] selected)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
    }

    private static async Task<string> Earn(Driver game, string no = "VEN·175")
    {
        await game.Act("P1", new PlayCardCommand("HOST", no, []));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.EquipmentPlayGrants);
        await Choose(game, "GEAR");
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.EquipmentPlayGrants);
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Graveyard);
        return PermissionPrefix + Assert.Single(game.State.EquipmentPlayGrants!).Id;
    }

    private static IReadOnlyList<ActionPromptChoiceDto> Choices(Driver game, string source)
    {
        var candidate = Assert.Single(game.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.PlayCard);
        var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var row = Assert.Single(rows, r => (string?)r["sourceObjectId"] == source);
        return Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(row["optionalCostChoices"]);
    }

    private static async Task Reject(Driver game, GameCommand command, string player = "P1")
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = game.PromptFor(player).PromptId; raw["snapshotTick"] = game.State.Tick;
        await game.ActRetained(new(player, "jayce-reject-" + game.Commands().Length,
            JsonSerializer.SerializeToElement(raw, Json)), false);
    }

    [Theory]
    [InlineData("VEN·175", 37878)]
    [InlineData("SFD·084/221", 33166)]
    public async Task BothPrintingsDestroyOnResolutionThenGrantOneLaterSevenManaEquipmentPlay(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id);
        var trigger = Assert.Single(RuleTextParser.Parse(card).Triggers);
        Assert.Equal(TriggerKinds.SourceUnitPlayedDestroyEquipmentGrantTurnPlay, trigger.Kind);
        Assert.Equal(7, trigger.MaximumPlayedCardManaCost);
        var game = new Driver(Position(no, 4));
        var permission = await Earn(game, no);
        Assert.Contains(Choices(game, "SEVEN"), c => c.Id == permission);
        var normal = new PlayCardCommand("SEVEN", "SFD·168/221", []);
        var ordinary = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, normal));
        Assert.Equal(7, ordinary.Cost!.Mana); Assert.False(ordinary.CanPay);
        var discounted = normal with { OptionalCosts = [permission] };
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, discounted));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Yellow]);
        await game.Act("P1", discounted);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
        Assert.Contains("SEVEN", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.EquipmentPlayGrants);
        await Reject(game, new PlayCardCommand("SECOND", "SFD·168/221", [], OptionalCosts: [permission]));
    }

    [Theory]
    [InlineData("VEN·175")]
    [InlineData("SFD·084/221")]
    public async Task DecliningDestructionLeavesEquipmentAndCreatesNoPermission(string no)
    {
        var game = new Driver(Position(no));
        await game.Act("P1", new PlayCardCommand("HOST", no, []));
        await Choose(game);
        Assert.Empty(game.State.StackItems);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.EquipmentPlayGrants);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TURN_EQUIPMENT_PLAY_GRANTED");
    }

    [Theory]
    [InlineData("VEN·175", "UNIT")]
    [InlineData("SFD·084/221", "UNIT")]
    [InlineData("VEN·175", "ENEMY")]
    [InlineData("SFD·084/221", "ENEMY")]
    public async Task PublicConfirmationRejectsUnitsAndEnemyEquipment(string no, string target)
    {
        var initial = AddHand(Position(no), "ENEMY", "SFD·059/221");
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Hand = initial.PlayerZones["P1"].Hand.Where(id => id != "ENEMY").ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ENEMY"] = initial.CardObjects["ENEMY"] with { OwnerId = "P2", ControllerId = "P2" } } };
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HOST", no, []));
        var choice = game.State.PendingCardChoice!;
        Assert.DoesNotContain(target, choice.LegalObjectIds);
        await Reject(game, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        await Choose(game, "GEAR"); await game.Top();
        Assert.Single(game.State.EquipmentPlayGrants!);
    }

    [Theory]
    [InlineData("power")]
    [InlineData("too-large")]
    [InlineData("unit")]
    [InlineData("duplicate")]
    [InlineData("forged")]
    [InlineData("graveyard")]
    public async Task InvalidPermissionUseCannotPayOrConsumeTheGrant(string kind)
    {
        var initial = Position(yellow: kind == "power" ? 0 : 2);
        if (kind == "graveyard") initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with {
                    Hand = initial.PlayerZones["P1"].Hand.Where(id => id != "SEVEN").ToArray(), Graveyard = ["SEVEN"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["SEVEN"] = new("P1", "GRAVEYARD") } };
        var game = new Driver(initial);
        var permission = await Earn(game);
        var source = kind == "too-large" ? "EXPENSIVE" : kind == "unit" ? "UNIT" : "SEVEN";
        var selections = kind == "duplicate" ? new[] { permission, permission }
            : kind == "forged" ? [PermissionPrefix + "forged"] : [permission];
        await Reject(game, new PlayCardCommand(source, game.State.CardObjects[source].CardNo!, [], OptionalCosts: selections));
        Assert.Single(game.State.EquipmentPlayGrants!);
    }

    [Fact]
    public async Task PlayingAnEquipmentNormallyPreservesTheOptionalPermission()
    {
        var game = new Driver(Position()); var permission = await Earn(game);
        await game.Act("P1", new PlayCardCommand("SECOND", "SFD·168/221", []));
        Assert.Equal(9, game.State.RunePools["P1"].Mana);
        Assert.Single(game.State.EquipmentPlayGrants!);
        await game.Act("P1", new PlayCardCommand("SEVEN", "SFD·168/221", [], OptionalCosts: [permission]));
        Assert.Equal(9, game.State.RunePools["P1"].Mana); Assert.Null(game.State.EquipmentPlayGrants);
    }

    [Fact]
    public async Task TheGrantedPermissionSurvivesTheSourceReturningToHand()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(initial); var permission = await Earn(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.Single(game.State.EquipmentPlayGrants!);
        await game.Act("P1", new PlayCardCommand("SEVEN", "SFD·168/221", [], OptionalCosts: [permission]));
        Assert.Null(game.State.EquipmentPlayGrants);
    }

    [Fact]
    public async Task TwoSeparateDestructionsGrantTwoIndependentlyConsumablePermissions()
    {
        var game = new Driver(AddHand(AddHand(Position(mana: 30, yellow: 3), "HOST2", "VEN·175"), "THIRD", "SFD·168/221"));
        var first = await Earn(game);
        await game.Act("P1", new PlayCardCommand("SEVEN", "SFD·168/221", []));
        await game.Act("P1", new PlayCardCommand("HOST2", "VEN·175", ["SEVEN"])); await game.Top();
        Assert.Equal(2, game.State.EquipmentPlayGrants!.Count);
        var second = PermissionPrefix + Assert.Single(game.State.EquipmentPlayGrants, g => PermissionPrefix + g.Id != first).Id;
        await game.Act("P1", new PlayCardCommand("SECOND", "SFD·168/221", [], OptionalCosts: [first]));
        Assert.Equal(second, PermissionPrefix + Assert.Single(game.State.EquipmentPlayGrants!).Id);
        await game.Act("P1", new PlayCardCommand("THIRD", "SFD·168/221", [], OptionalCosts: [second]));
        Assert.Null(game.State.EquipmentPlayGrants); Assert.Equal(15, game.State.RunePools["P1"].Mana);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GoldGrantsPermissionOnlyIfTheSkillActuallyDestroysIt(bool spentInResponse)
    {
        var game = new Driver(GoldPosition());
        await game.Act("P1", new PlayCardCommand("HOST", "VEN·175", ["GEAR"]));
        if (spentInResponse) {
            await game.Act("P1", new ActivateAbilityCommand("GEAR", P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, []));
            Assert.False(game.State.CardObjects.ContainsKey("GEAR"));
            Assert.Equal("GEAR", Assert.Single(Assert.Single(game.State.StackItems).DepartedTokenTargets!).Target.ObjectId);
        }
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("GEAR"));
        if (spentInResponse) {
            Assert.Null(game.State.EquipmentPlayGrants);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TURN_EQUIPMENT_PLAY_GRANTED");
        }
        else Assert.Single(game.State.EquipmentPlayGrants!);
    }

    [Theory]
    [InlineData("factory")]
    [InlineData("target")]
    [InlineData("generation")]
    [InlineData("tick")]
    [InlineData("missing")]
    [InlineData("null")]
    public async Task RecoveryRejectsMissingOrForgedDepartedTokenTargets(string field)
    {
        var game = new Driver(GoldPosition());
        await game.Act("P1", new PlayCardCommand("HOST", "VEN·175", ["GEAR"]));
        await game.Act("P1", new ActivateAbilityCommand("GEAR", P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, []));
        var item = Assert.Single(game.State.StackItems); var captured = Assert.Single(item.DepartedTokenTargets!);
        var bad = field switch {
            "factory" => captured with { TokenFactoryCardNo = "forged" },
            "target" => captured with { Target = captured.Target with { ObjectId = "invented" } },
            "generation" => captured with { Target = captured.Target with { Generation = 999 } },
            "tick" => captured with { DepartedTick = game.State.Tick + 1 }, _ => captured };
        item = item with { DepartedTokenTargets = field == "missing" ? null : field == "null" ? [null!] : [bad] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item] }));
    }

    [Fact]
    public async Task ReplacingTheDestructionWithBanishCreatesNoPermission()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { UntilEndOfTurnEffects = ["BANISH_IF_DESTROYED_THIS_TURN"] } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HOST", "VEN·175", ["GEAR"])); await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Banished);
        Assert.Null(game.State.EquipmentPlayGrants);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "TURN_EQUIPMENT_PLAY_GRANTED");
    }

    [Fact]
    public async Task UnusedPermissionExpiresWhenTheTurnEnds()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["P2-DRAW"] = OfficialGarenConquestBattlefieldCountTests.Printed("P2-DRAW", "OGN·001/298", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["P2-DRAW"] = new("P2", "MAIN_DECK") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { MainDeck = ["P2-DRAW"] } } };
        var game = new Driver(initial); var permission = await Earn(game);
        var turn = game.State.TurnNumber;
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(turn + 1, game.State.TurnNumber);
        Assert.Null(game.State.EquipmentPlayGrants);
        await game.Act("P2", new EndTurnCommand());
        await Reject(game, new PlayCardCommand("SEVEN", "SFD·168/221", [], OptionalCosts: [permission]));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("turn")]
    [InlineData("controller")]
    [InlineData("cap")]
    [InlineData("tick")]
    [InlineData("generation")]
    [InlineData("source")]
    [InlineData("duplicate")]
    [InlineData("null")]
    public async Task RecoveryRejectsForgedPermissions(string field)
    {
        var game = new Driver(Position()); await Earn(game); var state = game.State;
        var grant = Assert.Single(state.EquipmentPlayGrants!);
        var bad = field switch {
            "id" => grant with { Id = "forged" }, "turn" => grant with { TurnNumber = state.TurnNumber + 1 },
            "controller" => grant with { ControllerId = "P2" }, "cap" => grant with { MaximumManaCost = 99 },
            "tick" => grant with { CreatedTick = state.Tick + 1 },
            "generation" => grant with { DestroyedEquipment = grant.DestroyedEquipment with { Generation = 999 } },
            "source" => grant with { SourcePlay = grant.SourcePlay with { CardNo = "OGN·001/298" } }, _ => grant };
        state = state with { EquipmentPlayGrants = field == "duplicate" ? [bad, bad] : field == "null" ? [null!] : [bad] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state), e => e.Contains("equipment play permission"));
    }
}
