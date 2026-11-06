using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class LegendFieldSkillStackTests
{
    private const string Yasuo = LegendActionAbilityCatalog.YasuoLegendAbilityId;
    private const string Pyke = LegendActionAbilityCatalog.PykeLegendAbilityId;
    private const string Khazix = LegendActionAbilityCatalog.KhazixLegendMoveAbilityId;
    private const string Teemo = LegendActionAbilityCatalog.TeemoLegendAbilityId;
    private const string Poppy = LegendActionAbilityCatalog.PoppyLegendAbilityId;

    public static IEnumerable<object[]> Printings()
        => new[] { Yasuo, Pyke, Khazix, Teemo, Poppy }.SelectMany(ability =>
            LegendActionAbilityCatalog.SourceCardNosForAbility(ability).Select(card => new object[] { ability, card }));

    private static (string[] Costs, int Mana, int Experience) Cost(string ability)
        => ability == Poppy ? (["SPEND_EXPERIENCE:3"], 0, 3)
            : ability == Khazix ? (["SPEND_EXPERIENCE:2"], 0, 2)
            : ability == Yasuo ? (["SPEND_MANA:2"], 2, 0) : (["SPEND_MANA:1"], 1, 0);

    private static LegendActCommand Command(string ability, string target = "HOST", string source = "LEGEND")
        => new(source, ability, ability == Poppy ? [] : [target], Cost(ability).Costs);
    private static CardObjectState[] Gold(MatchState state)
        => state.CardObjects.Values.Where(card => card.Tags.Contains("金币")
            && state.PlayerZones["P1"].Base.Contains(card.ObjectId)).ToArray();

    private static MatchState Position(string ability, string? printing = null, bool inBase = false)
    {
        var state = PrintedExhaustAbilityTests.Position(ability == Teemo ? "OGN·121/298" : "OGN·219/298");
        var source = printing ?? LegendActionAbilityCatalog.SourceCardNosForAbility(ability).First();
        state = state with {
            PlayerExperience = new Dictionary<string, int>(state.PlayerExperience) { ["P1"] = 5 },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"],
                    MainDeck = new[] { "DRAW" }.Concat(state.PlayerZones["P1"].MainDeck).ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = new("LEGEND", cardNo: source, ownerId: "P1", controllerId: "P1", tags: ["传奇"]),
                ["DRAW"] = OfficialGarenConquestBattlefieldCountTests.Printed("DRAW", "OGN·219/298", "P1"),
                ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = ability == Khazix } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND"), ["DRAW"] = new("P1", "MAIN_DECK") } };
        return ability is Yasuo or Pyke or Khazix && !inBase
            ? LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F") : state;
    }

    [Theory]
    [MemberData(nameof(Printings))]
    public async Task EveryPrintingPaysAndExhaustsBeforeItsIndependentSkillResolves(string ability, string printing)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        Assert.Contains(catalog.Cards, card => card.CardNo == printing && card.CardEffect.Length > 0);
        var initial = Position(ability, printing); var game = new Driver(initial);
        await game.Act("P1", Command(ability));
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(20 - Cost(ability).Mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(5 - Cost(ability).Experience, game.State.PlayerExperience["P1"]);
        Assert.Equal(initial.ObjectLocations["HOST"], game.State.ObjectLocations["HOST"]);
        Assert.DoesNotContain("DRAW", game.State.PlayerZones["P1"].Hand); Assert.Empty(Gold(game.State));
        var item = Assert.Single(game.State.StackItems);
        if (ability == Poppy) { Assert.NotNull(item.LegendDiscardDraw); Assert.Empty(item.TargetObjectIds); }
        else { Assert.NotNull(item.LegendFieldEffect); Assert.Equal(["HOST"], item.TargetObjectIds); }
        await game.Top();
        if (ability == Poppy) Assert.Contains("DRAW", game.State.PlayerZones["P1"].Hand);
        else if (ability is Teemo or Pyke) Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        else {
            Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
            Assert.Equal(initial.CardObjects["HOST"].IsExhausted, game.State.CardObjects["HOST"].IsExhausted);
        }
        if (ability == Pyke) Assert.True(Assert.Single(Gold(game.State)).IsExhausted);
        else Assert.Empty(Gold(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task YasuoDeclaresABattlefieldBeforePaymentAndNeverChangesToBattlefieldToBattlefieldMovement(bool response)
    {
        var state = Position(Yasuo, inBase: true);
        if (response) state = AttackDamageDistributionTests.Spell(state, "MOVE", "VEN·034", "P1");
        var game = new Driver(state); await game.Act("P1", Command(Yasuo));
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("MOVEMENT_DESTINATION", choice.Kind);
        Assert.Contains(choice.Options, option => option.Id == "BATTLEFIELD:OTHER");
        Assert.Empty(game.State.StackItems); Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(20, game.State.RunePools["P1"].Mana);
        await game.Act("P1", new PayCostCommand(choice.Id, "RULE_REPLACEMENT", ["BATTLEFIELD:OTHER"]));
        Assert.Equal("BATTLEFIELD:OTHER", Assert.Single(game.State.StackItems).Destination);
        Assert.Equal("BASE", game.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(18, game.State.RunePools["P1"].Mana);
        if (response) {
            await game.Act("P1", new PlayCardCommand("MOVE", "VEN·034", ["F", "HOST"]));
            await game.Top(); Assert.Equal("F", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        }
        await game.Top();
        Assert.Equal(response ? "F" : "OTHER", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal(1, game.State.CardObjects["HOST"].MovementCountThisTurn);
        await game.Replay();
    }

    [Theory]
    [InlineData(Pyke)] [InlineData(Khazix)] [InlineData(Teemo)]
    public async Task AResponseReturningTheTargetCannotAffectItsNewGenerationAndDoesNotPreventTheIndependentCoin(string ability)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(ability), "RETURN", "OGN·104/298", "P1");
        var generation = initial.CardObjects["HOST"].ObjectGeneration;
        var game = new Driver(initial); await game.Act("P1", Command(ability));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top(); Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > generation);
        await game.Top(); Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(5 - Cost(ability).Experience, game.State.PlayerExperience["P1"]);
        Assert.Equal(ability == Pyke ? 1 : 0, Gold(game.State).Length);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task PykeReturnsToTheOwnerAndRebuildsTheUnitWhileDetachingItsEquipment()
    {
        var state = Position(Pyke);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { OwnerId = "P2", Damage = 1, IsEmpowered = true,
                BoonCount = 1, Tags = state.CardObjects["HOST"].Tags.Append(CardObjectTags.Boon).ToArray(),
                UntilEndOfTurnEffects = ["ROAM"] } } };
        var generation = state.CardObjects["HOST"].ObjectGeneration;
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_RED"])); await game.Top();
        Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal("F", game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        await game.Act("P1", Command(Pyke)); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain("HOST", game.State.PlayerZones["P1"].Hand);
        var returned = game.State.CardObjects["HOST"];
        Assert.Equal("P2", returned.ControllerId); Assert.True(returned.ObjectGeneration > generation);
        Assert.Equal(0, returned.Damage); Assert.Equal(0, returned.BoonCount);
        Assert.False(returned.IsEmpowered); Assert.Empty(returned.UntilEndOfTurnEffects);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        // CN 719.5 detaches in place; CN 149.3/323.6 then recalls non-unit gear.
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.True(Assert.Single(Gold(game.State)).IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TeemoMayReturnItsOwnedUnitFromTheChampionZoneOrAnOpponentsControl(bool champion)
    {
        var state = Position(Teemo);
        if (champion) {
            state = CurrentControllerDamageBonusTests.Place(state, "HOST", "P1", "HAND");
            state = state with {
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                    ["P1"] = state.PlayerZones["P1"] with {
                        Hand = state.PlayerZones["P1"].Hand.Where(id => id != "HOST").ToArray(), ChampionZone = ["HOST"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["HOST"] = new("P1", "CHAMPION") } };
        } else {
            state = CurrentControllerDamageBonusTests.Place(state, "HOST", "P2", "BASE");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { ControllerId = "P2", UntilEndOfTurnEffects = ["COMBAT_KEYWORD:法盾2:1"] } } };
        }
        var power = state.RunePools["P1"].TotalPower; var game = new Driver(state);
        await game.Act("P1", Command(Teemo));
        Assert.Equal(power - (champion ? 0 : 2), game.State.RunePools["P1"].TotalPower);
        Assert.Equal(champion ? 0 : 2, Assert.Single(game.State.StackItems).LegendFieldEffect!.SpellshieldTaxPower);
        await game.Top(); Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.PlayerZones["P1"].ChampionZone);
        Assert.DoesNotContain("HOST", game.State.PlayerZones["P2"].Base);
        await game.Replay();
    }

    [Theory]
    [InlineData("experience")] [InlineData("ready")] [InlineData("base")]
    [InlineData("opponent")] [InlineData("enemy")] [InlineData("duplicate")]
    [InlineData("facedown")] [InlineData("wrong-cost")]
    public async Task IllegalKhazixCostsAndTargetsRejectWithoutMutation(string branch)
    {
        var state = Position(Khazix); var cmd = Command(Khazix);
        if (branch == "experience") state = state with { PlayerExperience = new Dictionary<string, int>(state.PlayerExperience) { ["P1"] = 1 } };
        if (branch == "base") state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BASE");
        if (branch is "ready" or "facedown") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsExhausted = branch != "ready", IsFaceDown = branch == "facedown" } } };
        if (branch == "enemy") cmd = Command(Khazix, "ENEMY");
        if (branch == "duplicate") cmd = cmd with { TargetObjectIds = ["HOST", "HOST"] };
        if (branch == "wrong-cost") cmd = cmd with { OptionalCosts = ["SPEND_EXPERIENCE:1"] };
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid-field-skill", branch == "opponent" ? "P2" : "P1", cmd.CmdType), cmd, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task CapturedFieldSkillsRejectForgedSourcesFeesBindingsAndDestinations()
    {
        var game = new Driver(Position(Khazix)); await game.Act("P1", Command(Khazix));
        var item = Assert.Single(game.State.StackItems); var context = item.LegendFieldEffect!;
        foreach (var forged in new[] {
            item with { LegendFieldEffect = null }, item with { OptionalCosts = [] },
            item with { Destination = "BATTLEFIELD:F" }, item with { TimingContext = TimingStates.SpellDuelOpen },
            item with { TargetGenerations = new Dictionary<string, long> { ["HOST"] = 99 } },
            item with { LegendFieldEffect = context with { AbilityId = Pyke } },
            item with { LegendFieldEffect = context with { CardNo = "FORGED" } },
            item with { LegendFieldEffect = context with { Source = context.Source with { Generation = 99 } } },
            item with { LegendFieldEffect = context with { CapturedTick = game.State.Tick + 1 } },
            item with { LegendFieldEffect = context with { FaceRevision = 99 } },
            item with { LegendFieldEffect = context with { SpellshieldTaxPower = 1 } },
            item with { LegendFieldEffect = context with { OriginZone = "BASE" } } })
            Assert.Contains("invalid captured legend field effect", OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
    }

    [Fact]
    public async Task PoppyBurnoutOccursOnlyWhenItsQueuedDrawResolves()
    {
        var state = Position(Poppy); var deck = state.PlayerZones["P1"].MainDeck;
        state = state with { PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 0 },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { MainDeck = [], Graveyard = state.PlayerZones["P1"].Graveyard.Concat(deck).ToArray() } },
            ObjectLocations = state.ObjectLocations.ToDictionary(pair => pair.Key,
                pair => deck.Contains(pair.Key) ? new ObjectLocationState("P1", "GRAVEYARD") : pair.Value) };
        var score = state.PlayerScores["P2"]; var game = new Driver(state);
        await game.Act("P1", Command(Poppy)); Assert.Equal(score, game.State.PlayerScores["P2"]);
        await game.Top(); Assert.Equal(score + 1, game.State.PlayerScores["P2"]);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "BURNOUT_APPLIED");
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }
}
