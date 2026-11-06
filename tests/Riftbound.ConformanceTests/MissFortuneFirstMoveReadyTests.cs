using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class MissFortuneFirstMoveReadyTests
{
    private const string Captain = "OGN·162/298";

    private static MatchState Board(bool target = true)
    {
        var state = ArmamentMightTests.Position(unit: Captain);
        var host = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", Captain, "P1");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Captain, out var behavior));
        state = state with { RoomId = "MISS-FORTUNE-FIRST-MOVE", CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = host with { Tags = host.Tags.Concat(behavior.SourceUnitTags.Split('|')).Distinct(StringComparer.Ordinal).ToArray() },
                ["GEAR"] = state.CardObjects["GEAR"] with { IsExhausted = target } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(30, 20, new Dictionary<string, int> { [RuneTrait.Orange] = 20 }), ["P2"] = new(30, 20) } };
        foreach (var player in new[] { "P1", "P2" })
        {
            var id = "DRAW-" + player;
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                    [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·001/298", player) },
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = state.PlayerZones[player] with {
                    MainDeck = [..state.PlayerZones[player].MainDeck, id] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "MAIN_DECK") } };
        }
        return state;
    }

    private static MatchState Target(MatchState state, string kind, string player, bool exhausted = true)
    {
        var no = kind switch { "rune" => "OGN·007/298", "legend" => "OGN·247/298", "equipment" => "SFD·022/221", _ => "OGN·219/298" };
        var tags = kind switch { "rune" => new[] { CardObjectTags.RuneCard, "COLOR:red" },
            "equipment" => [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], "legend" => [], _ => [CardObjectTags.UnitCard] };
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["TARGET"] = new("TARGET", cardNo: no, power: kind == "unit" ? 4 : 0,
                    isExhausted: exhausted, ownerId: player, controllerId: player, tags: tags) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [player] = kind == "legend"
                ? state.PlayerZones[player] with { LegendZone = ["TARGET"] }
                : state.PlayerZones[player] with { Base = [..state.PlayerZones[player].Base, "TARGET"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["TARGET"] = new(player, kind == "legend" ? "LEGEND" : "BASE") } };
    }

    private static Task Move(Driver game, bool toBase = false)
        => game.Act("P1", new MoveUnitCommand("HOST", toBase ? "BATTLEFIELD" : "BASE", toBase ? "BASE" : "BATTLEFIELD:F", []));

    private static async Task Choose(Driver game, string target = "GEAR")
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
    }

    [Fact]
    public async Task PrintedPlayPaysFiveManaAndOneOrangePowerAndOnlyMovingTriggersTheSkill()
    {
        var printed = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Captain);
        Assert.Equal(31388, printed.Id); Assert.Equal(5, printed.Energy); Assert.Equal(1, printed.ReturnEnergy); Assert.Equal(5, printed.Power);
        var spec = Assert.Single(RuleTextParser.Parse(printed).Triggers, t => t.Kind == TriggerKinds.UnitFirstMovementReadyOtherObject);
        Assert.False(spec.Optional); Assert.True(spec.OncePerTurn); Assert.Equal(1, spec.MovementOrdinal);
        Assert.Equal(TriggerTargetScopes.OtherExhaustedFieldObject, spec.TargetScope);
        var state = Board();
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = state.PlayerZones["P1"].Base.Where(id => id != "HOST").ToArray(), Hand = ["HOST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["HOST"] = new("P1", "HAND") } };
        state = AttackDamageDistributionTests.Spell(state, "READY", "UNL-009/219", "P1");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("HOST", Captain, []));
        Assert.Equal(25, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Orange]);
        Assert.Equal(5, game.State.CardObjects["HOST"].Power); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["HOST"])); await game.Top();
        await Move(game); await Choose(game); Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Top(); Assert.False(game.State.CardObjects["GEAR"].IsExhausted); await game.Replay();
    }

    [Theory]
    [InlineData("unit", "P1")] [InlineData("unit", "P2")]
    [InlineData("equipment", "P1")] [InlineData("equipment", "P2")]
    [InlineData("rune", "P1")] [InlineData("rune", "P2")]
    [InlineData("legend", "P1")] [InlineData("legend", "P2")]
    public async Task FirstMoveReadiesAnotherPublicObjectOfEitherPlayerAfterResponses(string kind, string player)
    {
        var game = new Driver(Target(Board(), kind, player)); await Move(game);
        var item = Assert.Single(game.State.StackItems); var context = Assert.IsType<ReadyObjectTriggerContext>(item.ReadyObjectTrigger);
        Assert.Equal("FIRST_MOVE", context.Timing); Assert.Equal(1, context.MovementOrdinal);
        Assert.Equal("HOST", context.Movement!.ObjectId); Assert.Equal("P1", context.Movement.ControllerId);
        Assert.Equal(1, game.State.CardObjects["HOST"].MovementCountThisTurn);
        await Choose(game, "TARGET"); Assert.True(game.State.CardObjects["TARGET"].IsExhausted);
        await game.Top(); Assert.False(game.State.CardObjects["TARGET"].IsExhausted);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted); await game.Replay();
    }

    [Theory]
    [InlineData("empty")] [InlineData("self")] [InlineData("active")] [InlineData("hand")]
    public async Task MandatoryChoiceRejectsOmissionTheSourceAndOtherIllegalObjectsAtomically(string kind)
    {
        var game = new Driver(Board()); await Move(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        var target = kind switch { "self" => "HOST", "active" => "F", "hand" => "D0", _ => null };
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, target is null ? [] : [target]));
        await Choose(game); await game.Top();
    }

    [Fact]
    public async Task AFirstMovementWithoutAnyLegalTargetStillConsumesTheOccurrence()
    {
        var state = Target(Board(false), "rune", "P1", false);
        state = AttackDamageDistributionTests.Spell(state, "READY", "UNL-009/219", "P1");
        var game = new Driver(state); await Move(game);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(1, game.State.CardObjects["HOST"].MovementCountThisTurn);
        await game.Act("P1", new TapRuneCommand("TARGET"));
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["HOST"])); await game.Top();
        await Move(game, true); Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(2, game.State.CardObjects["HOST"].MovementCountThisTurn);
        Assert.True(game.State.CardObjects["TARGET"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task AnotherTurnResetsFirstMovementForTheSamePhysicalUnit()
    {
        var game = new Driver(Target(Board(false), "rune", "P1"));
        await Move(game); await Choose(game, "TARGET"); await game.Top();
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Equal(0, game.State.CardObjects["HOST"].MovementCountThisTurn);
        await game.Act("P1", new TapRuneCommand("TARGET")); await Move(game, true); await Choose(game, "TARGET");
        await game.Top(); Assert.False(game.State.CardObjects["TARGET"].IsExhausted); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task EnemyWardUsesTheSharedTargetPaymentAndAllowsDecliningThatCost(bool pay)
    {
        var state = Target(Board(false), "unit", "P2");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TARGET"] = state.CardObjects["TARGET"] with { Tags = [..state.CardObjects["TARGET"].Tags, "法盾2"] } } };
        var game = new Driver(state); await Move(game); var before = game.State.RunePools["P1"].TotalPower;
        await Choose(game, "TARGET"); var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(2, payment.PowerCost);
        foreach (var forged in new[] { 0, 1, 3 })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { PendingPayment = payment with { PowerCost = forged } }));
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        if (pay) await game.Top();
        Assert.Equal(before - (pay ? 2 : 0), game.State.RunePools["P1"].TotalPower);
        Assert.Equal(!pay, game.State.CardObjects["TARGET"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task AnOpponentsMovementStillTriggersTheUnitControllersSkill()
    {
        var state = LocalDestructionRecallTests.At(Board(), "HOST", "P1", "BATTLEFIELD", "F");
        state = AttackDamageDistributionTests.Spell(state, "MOVE", "OGN·043/298", "P2") with {
            TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(state);
        await game.Act("P2", new PlayCardCommand("MOVE", "OGN·043/298", ["HOST"])); await game.Top();
        var item = Assert.Single(game.State.StackItems); Assert.Equal("P1", item.ControllerId);
        Assert.Equal("P2", item.ReadyObjectTrigger!.Movement!.MovingPlayerId);
        await Choose(game); await game.Top(); Assert.False(game.State.CardObjects["GEAR"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task CopiedFirstMoveSkillsResolveAfterSourceLeavesAndGrantingEquipmentDetaches()
    {
        var state = Target(Board(), "unit", "P2");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = state.CardObjects["GEAR"] with { CardNo = "SFD·059/221" },
                ["TARGET"] = state.CardObjects["TARGET"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(30, 20, new Dictionary<string, int> { [RuneTrait.Green] = 20, [RuneTrait.Orange] = 20 }) } };
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await Move(game); Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Single(game.State.StackItems, item => item.ReadyObjectTrigger!.Origin is not null);
        await Choose(game, "TARGET");
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(2, payment.PowerCost);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.NotNull(game.State.PendingCardChoice); await Choose(game);
        Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingPayment);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        var count = game.Events().Length;
        await game.Top(); await game.Top();
        Assert.False(game.State.CardObjects["TARGET"].IsExhausted); Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(game.Events().Skip(count))); await game.Replay();
    }

    [Theory]
    [InlineData("HOST")] [InlineData("TARGET")]
    public async Task ReturningSourcePreservesTheSkillButReturningTargetInvalidatesItsBinding(string returned)
    {
        var state = AttackDamageDistributionTests.Spell(Target(Board(false), "unit", "P1"), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state); await Move(game); await Choose(game, "TARGET");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned])); await game.Top();
        Assert.Contains(returned, game.State.PlayerZones["P1"].Hand);
        var opponent = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(game.State)["P2"], new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.False(opponent.GetProperty("players").GetProperty("P1").GetProperty("objects").TryGetProperty(returned, out _));
        if (returned == "HOST") Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        var count = game.Events().Length; var targetBefore = game.State.CardObjects["TARGET"];
        await game.Top();
        if (returned == "TARGET") Assert.Equal(targetBefore, game.State.CardObjects["TARGET"]);
        else Assert.False(game.State.CardObjects["TARGET"].IsExhausted);
        if (returned == "HOST") Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(game.Events().Skip(count)));
        else Assert.DoesNotContain(game.Events().Skip(count), e => e.Event.Kind == "UNIT_READIED");
        await game.Replay();
    }

    [Theory]
    [InlineData("ordinal")] [InlineData("unit")] [InlineData("controller")]
    [InlineData("timing")] [InlineData("movement")] [InlineData("optional-choice")]
    public async Task RecoveryRejectsForgedMovementFactsAndOptionalChoices(string mutation)
    {
        var game = new Driver(Board()); await Move(game);
        var item = Assert.Single(game.State.StackItems); var c = item.ReadyObjectTrigger!;
        var changed = c with { MovementOrdinal = mutation == "ordinal" ? 2 : c.MovementOrdinal,
            Movement = mutation switch { "unit" => c.Movement! with { ObjectId = "GEAR" },
                "controller" => c.Movement! with { ControllerId = "P2" }, "movement" => null, _ => c.Movement },
            Timing = mutation == "timing" ? "SOURCE_PLAY" : c.Timing };
        var forged = mutation == "optional-choice" ? game.State with { PendingCardChoice = game.State.PendingCardChoice! with { RequiredCount = 0 } }
            : game.State with { StackItems = [item with { ReadyObjectTrigger = changed }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-captain-" + state.Tick, player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
