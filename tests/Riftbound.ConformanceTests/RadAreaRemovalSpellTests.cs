using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadAreaRemovalSpellTests
{
    private const string Avalanche = "RAD·079/167";
    private const string Demacia = "RAD·117/167";
    private const string Balefire = "RAD·124/167";

    public static IEnumerable<object[]> Cards() => new object[][] {
        [Avalanche, 39578L, "雪崩", 5, 2, "对所有基地里的所有单位各造成3点伤害。"],
        [Demacia, 39622L, "为了德玛西亚！", 3, 0,
            "{{反应}}（可在任意时机打出，甚至先于其他法术和技能的结算。）\n选择一处战场。给予该处的友方单位在本回合内{{坚守2}}。（只要他们是防守方，便{{S}}+2。）"],
        [Balefire, 39629L, "冥火销魂", 3, 1,
            "放逐一名不高于3{{S}}的单位或一件装备。{{燃烧2}}。（将你主牌堆顶部的两张牌放入你的废牌堆。）"] };

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task EachPrintingUsesItsExactOfficialTextAndCosts(
        string no, long id, string name, int mana, int power, string text)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(name, card.CardName); Assert.Equal(text, card.CardEffect);
        Assert.Equal(mana, card.Energy); Assert.Equal(power, card.ReturnEnergy ?? 0); Assert.Null(card.Power);
        Assert.Equal("法术", card.CardCategoryName);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var definition));
        Assert.Equal(mana, definition.ManaCost); Assert.Equal(power, PrintedPowerCostRules.ForCard(no).Amount);
        Assert.Equal(no == Demacia, definition.CanPlayDuringPriority);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AvalancheDamagesBothBasesWithoutTargetingUnitsOrEquipment(bool ward)
    {
        var initial = Position(Avalanche);
        if (ward) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { Tags = [CardObjectTags.UnitCard, "法盾3"] },
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Tags = [CardObjectTags.UnitCard, "法盾3"] } } };
        initial = Add(initial, "FRAGILE", "P2", "BASE", power: 3);
        var game = new Driver(initial);
        var command = new PlayCardCommand("UNIT", Avalanche, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(5, quote.Cost!.Mana);
        Assert.Equal(2, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", command);
        Assert.Equal(15, game.State.RunePools["P1"].Mana); Assert.Equal(18, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, game.State.CardObjects["ALLY"].Damage);
        await game.Top();
        Assert.Equal(3, game.State.CardObjects["ALLY"].Damage); Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage);
        Assert.Contains("FRAGILE", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(new[] { "ALLY", "ENEMY", "FRAGILE" }, game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED")
            .Select(e => (string)e.Event.Payload["targetObjectId"]!).Order().ToArray());
        foreach (var id in new[] { "FIELD", "RUNE", "GEAR", "GRAVE" }) Assert.Equal(0, game.State.CardObjects[id].Damage);
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task AvalancheCollectsBaseMembershipWhenItResolvesAndCanResolveWithEmptyBases()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(Avalanche), "RETURN", "OGN·104/298", "P1");
        initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Avalanche, []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand); Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage); await game.Replay();
    }

    [Fact]
    public async Task AvalancheCapturesDamageBonusesBeforeItsFragileBonusSourceDies()
    {
        var initial = HandDiscardObserverTests.Put(Position(Avalanche), "ANNIE", "OGS·001/024", "P1", "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "ANNIE", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Avalanche, [])); await game.Top();
        var damage = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED").Select(e => e.Event).ToArray();
        Assert.Equal(3, damage.Length);
        Assert.All(damage, e => Assert.Equal(4, Convert.ToInt32(e.Payload["damage"])));
        Assert.Contains("ANNIE", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DemaciaGrantsOnlyCurrentFriendlyUnitsAtTheChosenBattlefieldAndExpires(bool twice)
    {
        var initial = RadUnitEffectTestFixture.Place(Position(Demacia), "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = Add(initial, "BASE-ALLY", "P1", "BASE");
        initial = Add(initial, "STOLEN", "P2", "BASE");
        initial = RadUnitEffectTestFixture.Place(initial, "STOLEN", "P1", "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["FIELD"] = initial.CardObjects["FIELD"] with { Tags = [CardObjectTags.UnitCard, "法盾3"] } } };
        if (twice) initial = AttackDamageDistributionTests.Spell(initial, "SECOND", Demacia, "P1");
        var game = new Driver(initial);
        var command = new PlayCardCommand("UNIT", Demacia, ["BF"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", command); Assert.Equal(0, Steadfast(game.State, "ALLY")); await game.Top();
        if (twice) { await game.Act("P1", new PlayCardCommand("SECOND", Demacia, ["BF"])); await game.Top(); }
        foreach (var id in new[] { "ALLY", "FIELD", "STOLEN" }) {
            Assert.Equal(twice ? 4 : 2, Steadfast(game.State, id)); Assert.Equal(4, game.State.CardObjects[id].Power); }
        foreach (var id in new[] { "BASE-ALLY", "GEAR", "ENEMY", "RUNE" }) Assert.Equal(0, Steadfast(game.State, id));
        Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
        var combatState = RadUnitEffectTestFixture.Place(game.State, "ENEMY", "P2", "BATTLEFIELD", "BF");
        var battle = new BattleState(true, "battle:BF", "BF", ["ENEMY"], ["ALLY", "FIELD", "STOLEN"],
            new Dictionary<string, string> { ["ENEMY"] = "P2", ["ALLY"] = "P1", ["FIELD"] = "P1", ["STOLEN"] = "P1" });
        foreach (var id in battle.DefenderObjectIds)
            Assert.Equal(twice ? 8 : 6, CoreRuleEngine.ResolveAssignmentBattleCombatPower(combatState, battle, id, out _, out _));
        Assert.Equal(4, CoreRuleEngine.ResolveAssignmentBattleCombatPower(combatState, battle, "ENEMY", out _, out _));
        await game.Act("P1", new EndTurnCommand());
        foreach (var id in new[] { "ALLY", "FIELD", "STOLEN" }) Assert.Equal(0, Steadfast(game.State, id));
        await game.Replay();
    }

    [Fact]
    public async Task DemaciaCanChooseAnEmptyEnemyBattlefield()
    {
        var game = new Driver(Position(Demacia));
        Assert.Contains("OTHER", Targets(game.State));
        await game.Act("P1", new PlayCardCommand("UNIT", Demacia, ["OTHER"])); await game.Top();
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "STATUS_EFFECT_APPLIED");
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task DemaciaDoesNotFollowAUnitThatWasReturnedInResponse()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(Demacia), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Demacia, ["BF"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["FIELD"])); await game.Top();
        await game.Top(); Assert.Contains("FIELD", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, Steadfast(game.State, "FIELD")); await game.Replay();
    }

    public static IEnumerable<object[]> BanishTargets() => new[] { "ALLY", "GEAR" }.SelectMany(id =>
        new[] { "P1", "P2" }.SelectMany(player => new[] { "BASE", "BATTLEFIELD" }.Select(zone => new object[] { id, player, zone })));

    [Theory]
    [MemberData(nameof(BanishTargets))]
    public async Task BalefireBanishesEitherSidesUnitOrAnyPowerEquipmentBeforeBurning(
        string target, string controller, string zone)
    {
        var initial = Position(Balefire);
        initial = RadUnitEffectTestFixture.Place(initial, target, controller, zone, zone == "BATTLEFIELD" ? "OTHER" : null);
        if (target == "GEAR" && zone == "BATTLEFIELD") {
            initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", controller, "BATTLEFIELD", "OTHER");
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [target] = initial.CardObjects[target] with { AttachedToObjectId = "ENEMY" } } }; }
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [target] = initial.CardObjects[target] with { Power = target == "GEAR" ? 99 : 3,
                Tags = [..initial.CardObjects[target].Tags, "法盾"] } } };
        var deck = initial.PlayerZones["P1"].MainDeck.ToArray();
        Assert.Contains(target, Targets(initial));
        var game = new Driver(initial);
        var command = new PlayCardCommand("UNIT", Balefire, [target]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(controller == "P2" ? 2 : 1, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", command);
        Assert.DoesNotContain(target, game.State.PlayerZones["P1"].Banished);
        Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck); await game.Top();
        Assert.Contains(target, game.State.PlayerZones[initial.CardObjects[target].OwnerId!].Banished);
        Assert.Equal("BANISHED", game.State.ObjectLocations[target].Zone);
        Assert.Equal(deck.Skip(2), game.State.PlayerZones["P1"].MainDeck);
        Assert.All(deck.Take(2), id => Assert.Contains(id, game.State.PlayerZones["P1"].Graveyard));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "UNIT_DESTROYED" or "EQUIPMENT_DESTROYED" or "CARD_DRAWN");
        var effects = game.Events().Where(e => e.Event.Kind is "UNIT_BANISHED" or "CARDS_BURNED").Select(e => e.Event.Kind).ToArray();
        Assert.Equal(new[] { "UNIT_BANISHED", "CARDS_BURNED", "CARDS_BURNED" }, effects); await game.Replay();
    }

    [Fact]
    public async Task BalefireCannotBanishAUnitWhoseCurrentMightRoseAboveThreeButStillBurns()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(Balefire), "BUFF", "OGN·207/298", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { Power = 3 } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Balefire, ["ALLY"]));
        await game.Act("P1", new PlayCardCommand("BUFF", "OGN·207/298", ["ALLY"])); await game.Top();
        Assert.Equal(6, game.State.CardObjects["ALLY"].Power); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.Empty(game.State.PlayerZones["P1"].Banished);
        Assert.Single(game.State.PlayerZones["P1"].MainDeck); Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "CARDS_BURNED"));
        await game.Replay();
    }

    [Fact]
    public async Task BalefireDoesNotBanishAReturnedTargetAndItsLaterBurnStillExecutes()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(Balefire), "RETURN", "SFD·135/221", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Balefire, ["GEAR"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "SFD·135/221", ["GEAR"])); await game.Top(); await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand); Assert.Empty(game.State.PlayerZones["P1"].Banished);
        Assert.Single(game.State.PlayerZones["P1"].MainDeck); await game.Replay();
    }

    [Fact]
    public async Task BalefiresBurnoutCannotRecycleTheCardBanishedByItsPreviousInstruction()
    {
        var initial = Position(Balefire);
        foreach (var id in initial.PlayerZones["P1"].MainDeck.Skip(1).ToArray())
            initial = RadUnitEffectTestFixture.Place(initial, id, "P1", "GRAVEYARD");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Balefire, ["GEAR"])); await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Banished);
        Assert.DoesNotContain("GEAR", game.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain("GEAR", game.State.PlayerZones["P1"].Graveyard);
        Assert.Single(game.Events(), e => e.Event.Kind == "BURNOUT_APPLIED");
        var kinds = game.Events().Select(e => e.Event.Kind).ToArray();
        Assert.True(Array.IndexOf(kinds, "UNIT_BANISHED") < Array.IndexOf(kinds, "BURNOUT_APPLIED"));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN"); await game.Replay();
    }

    [Fact]
    public async Task EchoRepeatsBanishThenBurnWithSeparatelyChosenTargets()
    {
        var initial = Position(Balefire);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { Power = 3 } },
            UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(initial.UntilEndOfTurnEffects, "P1") };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Balefire, ["GEAR"], OptionalCosts: ["ECHO"],
            RepeatChoices: [new("", ["ALLY"])])); await game.Top();
        Assert.Equal(new[] { "ALLY", "GEAR" }, game.State.PlayerZones["P1"].Banished.Order().ToArray());
        var instructions = game.Events().Where(e => e.Event.Kind is "UNIT_BANISHED" or "CARDS_BURNED").Select(e => e.Event.Kind).ToArray();
        var once = new[] { "UNIT_BANISHED", "CARDS_BURNED", "CARDS_BURNED" };
        Assert.Equal(once.Concat(once), instructions);
        Assert.Single(game.State.PlayerZones["P1"].Graveyard, id => id == "UNIT");
        Assert.Equal(14, game.State.RunePools["P1"].Mana); Assert.Equal(18, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Theory]
    [InlineData(Avalanche)] [InlineData(Demacia)] [InlineData(Balefire)]
    public async Task CounteringTheSpellPreventsItsWholeEffect(string no)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(no), "COUNTER", "OGN·064/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, DefaultTargets(no)));
        var parent = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [parent.StackItemId])); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Equal(0, game.State.CardObjects["ALLY"].Damage);
        Assert.Equal(0, Steadfast(game.State, "FIELD")); Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, game.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(game.State.PlayerZones["P1"].Banished); await game.Replay();
    }

    public static IEnumerable<object[]> Rejections() => Cards().SelectMany(row =>
        new[] { "no-mana", "enemy-source", "graveyard", "wrong-turn", "extra-target" }
            .Concat((string)row[0] == Demacia ? [] : new[] { "no-power" })
            .Select(branch => new object[] { row[0], branch }));

    [Theory]
    [MemberData(nameof(Rejections))]
    public async Task IllegalPaymentOwnershipZoneTimingAndTargetCountsCannotChangeState(string no, string branch)
    {
        var state = Position(no);
        if (branch is "no-mana" or "no-power") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = branch == "no-mana" ? new(0, 20) : new(20, 0) } };
        if (branch == "enemy-source") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P2", "HAND");
        if (branch == "graveyard") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "GRAVEYARD");
        if (branch == "wrong-turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        await Reject(state, new PlayCardCommand("UNIT", no, branch == "extra-target" ? [..DefaultTargets(no), "ALLY"] : DefaultTargets(no)));
    }

    [Theory]
    [InlineData(Demacia, "ALLY")] [InlineData(Demacia, "GEAR")] [InlineData(Demacia, "UNKNOWN")]
    [InlineData(Balefire, "ALLY")] [InlineData(Balefire, "ENEMY")] [InlineData(Balefire, "GRAVE")]
    [InlineData(Balefire, "RUNE")] [InlineData(Balefire, "BF")] [InlineData(Balefire, "P1-DRAW-1")]
    public async Task InvalidTargetTypesLocationsAndCurrentMightAreNeitherOfferedNorAccepted(string no, string target)
    {
        var state = Position(no); Assert.DoesNotContain(target, Targets(state));
        await Reject(state, new PlayCardCommand("UNIT", no, [target]));
    }

    [Fact]
    public async Task HiddenEquipmentIsNeitherOfferedNorAccepted()
    {
        var state = Position(Balefire);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { IsFaceDown = true } } };
        Assert.DoesNotContain("GEAR", Targets(state)); await Reject(state, new PlayCardCommand("UNIT", Balefire, ["GEAR"]));
    }

    [Fact]
    public async Task OnlyDemaciaCanReactDuringTheOpponentsSpellChain()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(Demacia), "INCOMING", "VEN·049", "P2")
            with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        initial = AttackDamageDistributionTests.Spell(initial, "AVALANCHE", Avalanche, "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "BALEFIRE", Balefire, "P1");
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("INCOMING", "VEN·049", [])); await game.Act("P2", new PassPriorityCommand());
        await Reject(game.State, new PlayCardCommand("AVALANCHE", Avalanche, []));
        await Reject(game.State, new PlayCardCommand("BALEFIRE", Balefire, ["GEAR"]));
        await game.Act("P1", new PlayCardCommand("UNIT", Demacia, ["BF"])); await game.Top();
        Assert.Equal(2, Steadfast(game.State, "FIELD")); Assert.Single(game.State.StackItems);
        await game.Top(); await game.Replay();
    }

    private static string[] DefaultTargets(string no) => no == Avalanche ? [] : [no == Demacia ? "BF" : "GEAR"];
    private static int Steadfast(MatchState state, string id) => 2 * state.CardObjects[id].UntilEndOfTurnEffects
        .Count(effect => effect.StartsWith("COMBAT_KEYWORD:坚守2:", StringComparison.Ordinal));
    private static IEnumerable<string> Targets(MatchState state) => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [])
        .Where(c => c.Action == CommandTypes.PlayCard && c.Sources?.Any(s => s.Id == "UNIT") == true)
        .SelectMany(c => c.Targets ?? []).Select(c => c.Id);

    private static MatchState Position(string no)
    {
        var state = Add(VenUnitEntryBasicTests.Position(no), "FIELD", "P1", "BATTLEFIELD", "BF");
        var rune = OfficialGarenConquestBattlefieldCountTests.Printed("RUNE", "UNL-R01", "P1");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["RUNE"] = rune },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["RUNE"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, "RUNE"] } } };
    }

    private static MatchState Add(MatchState state, string id, string owner, string zone, string? field = null, int power = 4)
    {
        state = HandDiscardObserverTests.Put(state, id, "OGN·219/298", owner, "HAND");
        state = RadUnitEffectTestFixture.Place(state, id, owner, zone, field);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = state.CardObjects[id] with { Power = power } } };
    }

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.CanPay); Assert.Equal(hash, MatchStateHasher.Hash(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-rad-area", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
