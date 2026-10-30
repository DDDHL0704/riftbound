using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadMovementTriggerUnitTests
{
    private const string Witness = "RAD·074/167";
    private const string Elephant = "RAD·078/167";

    public static IEnumerable<object[]> Cards()
    {
        yield return [Witness, 39573L, 3, 3, 0];
        yield return [Elephant, 39577L, 6, 4, 1];
    }

    private static MatchState ElephantPosition(bool ward = false)
    {
        var state = AttackDamageDistributionTests.Position(Elephant);
        state = OfficialHoldSequenceTests.AddUnit(state, "REMOTE", "OGN·001/298", "OTHER", "P2");
        return state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key, p => p.Key switch {
            "HOST" => OfficialGarenConquestBattlefieldCountTests.Printed("HOST", Elephant, "P1") with {
                IsExhausted = false, Tags = [CardObjectTags.UnitCard, "COLOR:orange", "坚守3", "壁垒"] },
            "ENEMY1" or "REMOTE" => p.Value with { Power = 20,
                Tags = ward ? [..p.Value.Tags, "法盾2"] : p.Value.Tags }, _ => p.Value }) };
    }

    private static async Task Choose(Driver game, params string[] targets)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
    }

    private static MatchState WitnessPosition(string top, int power, bool empty = false)
    {
        var state = PakaaProtectorRevealTests.ControlledJournalPosition(top);
        var cards = state.CardObjects.ToDictionary();
        cards["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", Witness, "P1") with { IsExhausted = false };
        var deck = state.PlayerZones["P1"].MainDeck;
        cards[deck[0]] = cards[deck[0]] with { Power = power };
        if (!empty) return state with { CardObjects = cards };
        var locations = state.ObjectLocations.ToDictionary();
        foreach (var id in deck) locations[id] = new("P1", "GRAVEYARD");
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { MainDeck = [], Graveyard = [..state.PlayerZones["P1"].Graveyard, ..deck] } } };
    }

    private static async Task Move(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:" + game.State.BattlefieldStates.Keys.First()));
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    [Theory]
    [InlineData("OGN·103/298", 5, true)]
    [InlineData("OGN·103/298", 4, false)]
    [InlineData("OGN·104/298", 7, false)]
    [InlineData("SFD·022/221", 9, false)]
    public async Task WitnessDrawsOnlyTheRevealedPowerfulUnitAndOtherwiseRecycles(string top, int power, bool draw)
    {
        var state = WitnessPosition(top, power);
        var deck = state.PlayerZones["P1"].MainDeck.ToArray();
        var hand = state.PlayerZones["P1"].Hand.ToArray();
        var grave = state.PlayerZones["P1"].Graveyard.ToArray();
        var game = new Driver(state);
        await Move(game);
        Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(TriggerKinds.UnitMovedRevealTopDrawPowerfulUnitOrRecycle, Assert.Single(game.State.StackItems).MovementObserver!.Kind);
        await game.Top();
        Assert.Equal(draw ? deck.Skip(1) : deck.Skip(1).Append(deck[0]), game.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(draw ? hand.Append(deck[0]) : hand, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(grave, game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, game.State.CardObjects["HOST"].Power);
        Assert.DoesNotContain('"' + deck[1] + '"', JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        await game.Replay();
    }

    [Fact]
    public async Task WitnessEmptyDeckHasNoDrawBurnoutOrFollowup()
    {
        var game = new Driver(WitnessPosition("OGN·103/298", 5, true));
        var hand = game.State.PlayerZones["P1"].Hand.ToArray();
        await Move(game); await game.Top();
        Assert.Equal(hand, game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "CARD_DRAWN" or "BURNOUT_APPLIED" or "CARDS_REVEALED");
        await game.Replay();
    }

    [Fact]
    public async Task WitnessRejectsForgedCapturedKindAndGeneration()
    {
        var game = new Driver(WitnessPosition("OGN·103/298", 5)); await Move(game);
        var item = Assert.Single(game.State.StackItems);
        foreach (var context in new[] { item.MovementObserver! with { Kind = TriggerKinds.UnitMovedDraw },
            item.MovementObserver! with { SourceGeneration = item.MovementObserver.SourceGeneration + 1 } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { MovementObserver = context }] }));
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task OfficialWitnessTextAndPrintedStatsDriveItsSharedMovementSpec()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Witness);
        Assert.Equal(39573, card.Id); Assert.Equal(3, card.Energy); Assert.Equal(3, card.Power);
        var spec = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitMovedRevealTopDrawPowerfulUnitOrRecycle);
        Assert.Equal(5, spec.RequiredPowerThreshold); Assert.Equal(TriggerZones.MainDeck, spec.RevealMissDestinationZone);
    }

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task BothUnitsUseOfficialCostsAndNormalUnitEntry(string no, long id, int mana, int power, int rune)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(power, card.Power);
        Assert.Equal(rune, card.ReturnEnergy ?? 0);
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        var pool = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(pool.Mana - mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower - rune, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        if (no == Elephant) {
            Assert.Contains("坚守3", game.State.CardObjects["UNIT"].Tags);
            Assert.Contains("壁垒", game.State.CardObjects["UNIT"].Tags);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("ENEMY1")] [InlineData("REMOTE")] [InlineData("HOST")]
    public async Task ElephantCanDamageFriendlyOrEnemyUnitsOnAnyBattlefieldAfterResponse(string target)
    {
        var game = new Driver(ElephantPosition()); await Move(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("REMOTE", choice.LegalObjectIds); Assert.DoesNotContain("GEAR", choice.LegalObjectIds);
        await Choose(game, target); Assert.Equal(0, game.State.CardObjects[target].Damage);
        await game.Top(); Assert.Equal(3, game.State.CardObjects[target].Damage); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ElephantUsesTheSharedOptionalSpellshieldPayment(bool pay)
    {
        var game = new Driver(ElephantPosition(true)); await Move(game);
        var power = game.State.RunePools["P1"].TotalPower;
        await Choose(game, "REMOTE"); var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(2, payment.PowerCost);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        if (pay) await game.Top();
        Assert.Equal(pay ? power - 2 : power, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(pay ? 3 : 0, game.State.CardObjects["REMOTE"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData("GEAR")] [InlineData("F")] [InlineData("missing")]
    public async Task ElephantRejectsInvalidTargetsWithoutMutationAndMayDecline(string target)
    {
        var game = new Driver(ElephantPosition()); await Move(game);
        var choice = game.State.PendingCardChoice!;
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]);
        var before = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("bad-target", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await Choose(game); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task ElephantDamageIncludesSteadfastGrantedByAnEarlierSpell()
    {
        var state = AttackDamageDistributionTests.Spell(ElephantPosition(), "PARRY", "OGN·057/298", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("PARRY", "OGN·057/298", ["HOST"]));
        await game.Top(); Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage);
        await Move(game); await Choose(game, "ENEMY1");
        await game.Top(); Assert.Equal(6, game.State.CardObjects["ENEMY1"].Damage); await game.Replay();
    }

    [Fact]
    public async Task ElephantCannotReadSteadfastFromADepartedSource()
    {
        // CN359.3.e.12: information no longer available from a departed source is None.
        var state = AttackDamageDistributionTests.Spell(ElephantPosition(), "RETURN", "VEN·052", "P1");
        var game = new Driver(state); await Move(game); await Choose(game, "ENEMY1");
        await game.Act("P1", new PlayCardCommand("RETURN", "VEN·052", ["HOST"], Mode: "RETURN_FRIENDLY"));
        await game.Top(); Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Equal(0, game.State.CardObjects["ENEMY1"].Damage); await game.Replay();
    }
}
