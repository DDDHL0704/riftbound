using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class JayceReadyOtherObjectTests
{
    private static MatchState Position(string no, bool hand = true)
    {
        var state = hand ? EmpowerTests.Position(no) : AttachedRuleTextTests.Position(no, 0);
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 20, [RuneTrait.Red] = 20 }),
            ["P2"] = new(20, 20) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = state.CardObjects["GEAR"] with { IsExhausted = true } } };
    }

    private static async Task Confirm(Driver game, string? target = "GEAR")
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        if (target is not null) Assert.Contains(target, choice.LegalObjectIds);
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, target is null ? [] : [target]));
    }

    [Theory]
    [InlineData("VEN·068", 37760)] [InlineData("VEN·068a", 37761)]
    public async Task BothOfficialPrintingsPayBluePowerAndReadyOnlyAfterResponses(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(6, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(6, card.Power);
        var spec = Assert.Single(RuleTextParser.Parse(card).Triggers);
        Assert.Equal(TriggerKinds.SourcePlayedOrFirstEquipmentReadyOtherObject, spec.Kind);
        Assert.True(spec.Optional); Assert.True(spec.ExcludesTokens); Assert.Equal(1, spec.ObjectReadyCount);
        var game = new Driver(Position(no)); await game.Act("P1", new PlayCardCommand("HOST", no, []));
        Assert.Equal(14, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        Assert.Equal(6, game.State.CardObjects["HOST"].Power); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal("SOURCE_PLAY", Assert.Single(game.State.StackItems).ReadyObjectTrigger!.Timing);
        await Confirm(game); Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Top(); Assert.False(game.State.CardObjects["GEAR"].IsExhausted); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·068", "mana")] [InlineData("VEN·068a", "mana")]
    [InlineData("VEN·068", "trait")] [InlineData("VEN·068a", "target")]
    public async Task IllegalPlayCannotChangeState(string no, string kind)
    {
        var state = Position(no);
        if (kind is "mana" or "trait") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(kind == "mana" ? 5 : 20, 0, new Dictionary<string, int> { [kind == "mana" ? RuneTrait.Blue : RuneTrait.Red] = 1 }) } };
        var command = new PlayCardCommand("HOST", no, kind == "target" ? ["GEAR"] : []);
        await Reject(state, command);
    }

    [Theory]
    [InlineData("VEN·068")] [InlineData("VEN·068a")]
    public async Task FirstNonTokenEquipmentTriggersEvenAfterThePlayTriggerWasDeclinedAndSecondDoesNot(string no)
    {
        var state = AddEquipment(AddEquipment(Position(no), "E1"), "E2");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("HOST", no, [])); await Confirm(game, null);
        Assert.True(game.State.CardObjects["GEAR"].IsExhausted); Assert.Empty(game.State.StackItems);
        await game.Act("P1", new PlayCardCommand("E1", "SFD·016/221", []));
        Assert.Equal("FIRST_EQUIPMENT", Assert.Single(game.State.StackItems).ReadyObjectTrigger!.Timing);
        await Confirm(game, null); Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Act("P1", new PlayCardCommand("E2", "SFD·016/221", []));
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·068")] [InlineData("VEN·068a")]
    public async Task EquipmentPlayedBeforeThisUnitStillConsumesTheFirstEquipmentOccurrence(string no)
    {
        var game = new Driver(AddEquipment(AddEquipment(Position(no), "E1"), "E2"));
        await game.Act("P1", new PlayCardCommand("E1", "SFD·016/221", [])); Assert.Empty(game.State.StackItems);
        await game.Act("P1", new PlayCardCommand("HOST", no, [])); await Confirm(game, null);
        await game.Act("P1", new PlayCardCommand("E2", "SFD·016/221", []));
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); await game.Replay();
    }

    [Fact]
    public async Task PlayingTokenEquipmentDoesNotTriggerOrConsumeTheFirstNonTokenEquipment()
    {
        var state = AttackDamageDistributionTests.Spell(AddEquipment(Position("VEN·068", false), "E1"), "GOLD", "SFD·004/221", "P1");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("GOLD", "SFD·004/221", [])); await game.Top();
        Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is not null && c.Tags.Contains(CardObjectTags.EquipmentCard));
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        await game.Act("P1", new PlayCardCommand("E1", "SFD·016/221", []));
        Assert.Equal("FIRST_EQUIPMENT", Assert.Single(game.State.StackItems).ReadyObjectTrigger!.Timing);
        await Confirm(game, null); await game.Replay();
    }

    [Fact]
    public async Task OpponentsEquipmentDoesNotTriggerMyAbility()
    {
        var state = AddEquipment(Position("VEN·068", false), "E1", "P2") with {
            ActivePlayerId = "P2", TurnPlayerId = "P2" };
        var game = new Driver(state); await game.Act("P2", new PlayCardCommand("E1", "SFD·016/221", []));
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); await game.Replay();
    }

    [Fact]
    public async Task ANewTurnAllowsAnotherFirstEquipmentTrigger()
    {
        var state = EmpowerTests.Runes(AddEquipment(AddEquipment(Position("VEN·068", false), "E1"), "E2"), 2);
        foreach (var player in new[] { "P1", "P2" })
        {
            var id = "DRAW-" + player;
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = new(id, cardNo: "OGN·001/298", ownerId: player, controllerId: player, tags: [CardObjectTags.UnitCard]) },
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = state.PlayerZones[player] with { MainDeck = [..state.PlayerZones[player].MainDeck, id] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "MAIN_DECK") } };
        }
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("E1", "SFD·016/221", [])); await Confirm(game, null);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        foreach (var id in game.State.PlayerZones["P1"].Base.Where(id => game.State.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).ToArray())
            await game.Act("P1", new TapRuneCommand(id));
        await game.Act("P1", new PlayCardCommand("E2", "SFD·016/221", []));
        Assert.Equal("FIRST_EQUIPMENT", Assert.Single(game.State.StackItems).ReadyObjectTrigger!.Timing);
        await Confirm(game, null); await game.Replay();
    }

    [Theory]
    [InlineData("unit", "P1")] [InlineData("unit", "P2")]
    [InlineData("rune", "P1")] [InlineData("rune", "P2")]
    [InlineData("equipment", "P1")] [InlineData("legend", "P2")]
    public async Task CanReadyAnotherPublicExhaustedObjectOfEitherPlayer(string kind, string player)
    {
        var state = Position("VEN·068");
        var no = kind switch { "rune" => "OGN·007/298", "legend" => "OGN·247/298", "equipment" => "SFD·016/221", _ => "OGN·219/298" };
        var tags = kind switch { "rune" => new[] { CardObjectTags.RuneCard }, "equipment" => [CardObjectTags.EquipmentCard],
            "legend" => [], _ => [CardObjectTags.UnitCard] };
        var zones = state.PlayerZones[player];
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TARGET"] = new("TARGET", cardNo: no, power: kind == "unit" ? 4 : 0,
                isExhausted: true, ownerId: player, controllerId: player, tags: tags) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = kind == "legend"
                ? zones with { LegendZone = ["TARGET"] } : zones with { Base = [..zones.Base, "TARGET"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["TARGET"] = new(player, kind == "legend" ? "LEGEND" : "BASE") } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("HOST", "VEN·068", []));
        await Confirm(game, "TARGET"); await game.Top(); Assert.False(game.State.CardObjects["TARGET"].IsExhausted); await game.Replay();
    }

    [Theory]
    [InlineData("self")] [InlineData("active")] [InlineData("hidden")] [InlineData("graveyard")]
    public async Task InvalidReadyTargetDoesNotChangeThePendingChoice(string kind)
    {
        var state = Position("VEN·068a");
        if (kind == "active") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { IsExhausted = false } } };
        if (kind == "hidden") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { IsFaceDown = true, Tags = [CardObjectTags.EquipmentCard, CardObjectTags.Standby] } } };
        if (kind == "graveyard") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = state.PlayerZones["P1"].Base.Where(id => id != "GEAR").ToArray(), Graveyard = ["GEAR"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["GEAR"] = new("P1", "GRAVEYARD") } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("HOST", "VEN·068a", []));
        if (game.State.PendingCardChoice is null) { Assert.Empty(game.State.StackItems); return; }
        var choice = game.State.PendingCardChoice;
        await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [kind == "self" ? "HOST" : "GEAR"]));
    }

    [Theory]
    [InlineData("VEN·068")] [InlineData("VEN·068a")]
    public async Task SourceReturningToHandKeepsTheEarnedAbilityWithoutLeakingItsNewInstance(string no)
    {
        var state = AttackDamageDistributionTests.Spell(Position(no), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("HOST", no, [])); await Confirm(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        var count = game.Events().Length; await game.Top(); Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(game.Events().Skip(count))); await game.Replay();
    }

    [Theory]
    [InlineData("generation")] [InlineData("tick")] [InlineData("timing")] [InlineData("extra-target")]
    public async Task RecoveryRejectsForgedCapturedAbility(string mutation)
    {
        var game = new Driver(Position("VEN·068")); await game.Act("P1", new PlayCardCommand("HOST", "VEN·068", []));
        var item = Assert.Single(game.State.StackItems); var c = item.ReadyObjectTrigger!;
        var changed = item with { ReadyObjectTrigger = mutation switch {
            "generation" => c with { Source = c.Source with { Generation = c.Source.Generation + 1 } },
            "tick" => c with { CapturedTick = game.State.Tick + 1 },
            "timing" => c with { Timing = "FORGED" }, _ => c }, TargetObjectIds = mutation == "extra-target" ? ["GEAR"] : [] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [changed] }), e => e.Contains("ready other object"));
    }

    private static MatchState AddEquipment(MatchState state, string id, string player = "P1")
    {
        var zones = state.PlayerZones[player];
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = new(id, cardNo: "SFD·016/221", manaCost: 2, ownerId: player, controllerId: player, tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = zones with { Hand = [..zones.Hand, id] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "HAND") } };
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-jayce-" + state.Tick, "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
