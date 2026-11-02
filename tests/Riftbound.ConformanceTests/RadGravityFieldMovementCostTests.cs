using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ImposedMovementContinuationTests;

namespace Riftbound.ConformanceTests;

// Staged engine fixtures: the physical printing remains outside player decks
// until every effect-movement path and equipment Last Breath is implemented.
public sealed class RadGravityFieldMovementCostTests
{
    private const string Card = "RAD·062/167";

    [Theory]
    [InlineData(1, 1, false)] [InlineData(2, 1, false)]
    [InlineData(1, 2, false)] [InlineData(2, 2, true)]
    public async Task FixedFeesApplyOncePerInstructionAndStackWithPatrol(int units, int sources, bool patrol)
    {
        var state = Position(sources, patrol);
        var power = sources + (patrol ? units - 1 : 0);
        state = WithPool(state, new(sources, power));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("gravity-funded", "P1", CommandTypes.MoveUnit),
            Move(units), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].TotalPower);
        var cost = Assert.Single(result.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(sources, cost.Payload["manaCost"]); Assert.Equal(power, cost.Payload["powerCost"]);
        foreach (var id in units == 1 ? new[] { "A" } : ["A", "B"])
        {
            Assert.Equal("FIELD2", result.State.ObjectLocations[id].BattlefieldObjectId);
            Assert.True(result.State.CardObjects[id].IsExhausted);
        }
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(1, 0)]
    public async Task EitherMissingResourceRejectsWithoutSpendingTheOtherOrMoving(int mana, int power)
    {
        var state = WithoutRune(WithPool(Position(), new(mana, power)));
        await RadGearTestPosition.Reject(state, "P1", Move(1));
    }

    [Theory]
    [InlineData("friendly")] [InlineData("base")] [InlineData("hidden")] [InlineData("other-field")]
    public async Task OnlyVisibleOpponentSourcesAtTheDestinationImposeTheFee(string branch)
    {
        var state = Position();
        if (branch == "friendly") state = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == "TAX0" ? p.Value with { ControllerId = "P1", OwnerId = "P2" } : p.Value) };
        if (branch == "base") state = RadUnitEffectTestFixture.Place(state, "TAX0", "P2", "BASE");
        if (branch == "hidden") state = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == "TAX0" ? p.Value with { IsFaceDown = true } : p.Value) };
        if (branch == "other-field") state = RadUnitEffectTestFixture.Place(state, "TAX0", "P2", "BATTLEFIELD", "FIELD1");
        var result = await new CoreRuleEngine().ResolveAsync(state, new("gravity-inactive", "P1", CommandTypes.MoveUnit), Move(2), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "COST_PAID");
        Assert.Equal("FIELD2", result.State.ObjectLocations["A"].BattlefieldObjectId);
    }

    [Fact]
    public async Task ActualControllerRatherThanOwnerDeterminesWhoMustPay()
    {
        var state = WithPool(Position(), new(1, 1));
        state = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == "TAX0" ? p.Value with { OwnerId = "P1", ControllerId = "P2" } : p.Value) };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("gravity-controller", "P1", CommandTypes.MoveUnit), Move(1), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].TotalPower);
    }

    [Fact]
    public void PromptSeparatesFixedFeesFromTheFeeForEachExtraUnit()
    {
        var state = Position(2, true);
        var move = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.MoveUnit);
        var mana = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(move.Metadata!["manaPerMoveByDestination"]);
        var power = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(move.Metadata["powerPerMoveByDestination"]);
        var extra = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(move.Metadata["powerPerExtraUnitByDestination"]);
        Assert.Equal(2, mana["BATTLEFIELD:FIELD2"]); Assert.Equal(2, power["BATTLEFIELD:FIELD2"]);
        Assert.Equal(1, extra["BATTLEFIELD:FIELD2"]); Assert.Equal(0, mana["BATTLEFIELD:FIELD1"]);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task TappingThenRecyclingCanFundBothCostsAndDeclineRetainsBothResources(bool pay)
    {
        var initial = Position(); var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", Move(1));
        Assert.Equal(1, game.State.PendingRuleChoice!.Request.ManaCost);
        Assert.Equal(1, game.State.PendingRuleChoice.Request.PowerCost);
        Assert.DoesNotContain(game.State.PendingRuleChoice.Request.Options, o => o.Id == "PAY");
        await Decide(game, "TAP_RUNE:RUNE");
        Assert.Equal(1, game.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(game.State.PendingRuleChoice!.Request.Options, o => o.Id == "PAY");
        await Decide(game, "RECYCLE_RUNE:RUNE");
        Assert.Contains(game.State.PendingRuleChoice!.Request.Options, o => o.Id == "PAY");
        await Decide(game, pay ? "PAY" : "DECLINE");
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(pay ? "FIELD2" : null, game.State.ObjectLocations["A"].BattlefieldObjectId);
        Assert.Equal(pay, game.State.CardObjects["A"].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task AResolvingGroupSpellPaysOnceAndMayDeclineOnlyItsMovement(bool pay)
    {
        var initial = WithPool(Position(), new(5, 2));
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", "UNL-054/219", ["D", "D2"], Destination: "BATTLEFIELD:FIELD2"));
        await game.Top();
        Assert.Equal(1, game.State.PendingRuleChoice!.Request.ManaCost);
        Assert.Equal(1, game.State.PendingRuleChoice.Request.PowerCost);
        await Decide(game, pay ? "PAY" : "DECLINE");
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].TotalPower);
        foreach (var id in new[] { "D", "D2" }) Assert.Equal(pay ? "FIELD2" : "FIELD1", game.State.ObjectLocations[id].BattlefieldObjectId);
        Assert.Single(game.Events(), e => e.Event.Kind == (pay ? "COST_PAID" : "MOVEMENT_COST_DECLINED")
            && e.Event.Payload.GetValueOrDefault("manaCost") is int mana && mana == 1);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Fact]
    public async Task SpellOnlyManaCannotFundTheMovementFee()
    {
        var state = WithoutRune(WithPool(Position(), new(0, 1, spellOnlyMana: 1)));
        await RadGearTestPosition.Reject(state, "P1", Move(1));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ExactPowerAllocationKeepsTheManaFeeAndCancellationSpendsNeither(bool confirm)
    {
        var initial = WithPool(Position(), new(1, 1, new Dictionary<string, int> { [RuneTrait.Red] = 1 }));
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", Move(1));
        await Decide(game, "ALLOCATE_IMPOSED_MOVEMENT_POWER");
        Assert.Equal(1, game.State.PendingRuleChoice!.Request.ManaCost);
        var zeroWildcard = Assert.Single(game.State.PendingRuleChoice.Request.Options, o => o.Label == "0–0");
        await Decide(game, zeroWildcard.Id);
        Assert.Equal(1, game.State.PendingRuleChoice!.Request.ManaCost);
        Assert.Equal(1, game.State.RunePools["P1"].Mana); Assert.Equal(2, game.State.RunePools["P1"].TotalPower);
        await Decide(game, confirm ? "CONFIRM_IMPOSED_MOVEMENT_POWER" : "CANCEL_IMPOSED_MOVEMENT_POWER");
        if (!confirm)
        {
            Assert.Equal(1, game.State.RunePools["P1"].Mana); Assert.Equal(2, game.State.RunePools["P1"].TotalPower);
            await Decide(game, "DECLINE");
        }
        Assert.Equal(confirm ? 0 : 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(confirm ? 1 : 2, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(1, game.State.RunePools["P1"].Power);
        Assert.Equal(confirm ? 0 : 1, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    [Fact]
    public async Task PartialMovementSupportDoesNotAdmitThePrinting()
        => Assert.DoesNotContain((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Card);

    private static MoveUnitCommand Move(int units) => units == 1
        ? new("A", "BASE", "BATTLEFIELD:FIELD2")
        : new("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: ["A", "B"]);

    private static MatchState Position(int sources = 1, bool patrol = false)
    {
        var state = ImposedMovementPaymentTests.Position();
        if (!patrol) state = RadUnitEffectTestFixture.Place(state, "PATROL", "P2", "GRAVEYARD");
        for (var n = 0; n < sources; n++)
        {
            var id = "TAX" + n;
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = new(id, cardNo: Card, ownerId: "P2", controllerId: "P2",
                    tags: [CardObjectTags.EquipmentCard, "部署"]) } };
            state = RadUnitEffectTestFixture.Place(state, id, "P2", "BATTLEFIELD", "FIELD2");
        }
        return state;
    }

    private static MatchState WithoutRune(MatchState state) => state with {
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Base = ["A", "B"], RuneDeck = ["RUNE"] } },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["RUNE"] = new("P1", "RUNE_DECK") } };
}
