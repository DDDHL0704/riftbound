using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SiphoningStrikeTests
{
    private const string Card = "VEN·146";
    private static PlayCardCommand Play(string target = "ENEMY") => new("SPELL", Card, [target]);

    [Fact]
    public async Task OfficialPrintRetainsItsManaOnlyCostAndBattlefieldRestriction()
    {
        var printed = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Card);
        Assert.Equal(37849, printed.Id); Assert.Equal(4, printed.Energy); Assert.Null(printed.ReturnEnergy);
        var initial = Position();
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, Play()));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(4, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
    }

    [Theory]
    [InlineData(6, false)] [InlineData(7, false)] [InlineData(8, false)]
    [InlineData(6, true)] [InlineData(7, true)]
    public async Task PrintedDamagePaysThenWaitsAndActualImmediateOrLaterDeathEarnsAnAnswerableDormantRune(int runes, bool later)
    {
        var initial = Position(runes, later ? 12 : 3); var game = new Driver(initial);
        await game.Act("P1", Play());
        Assert.Equal(16, game.State.RunePools["P1"].Mana);
        Assert.Equal(initial.RunePools["P1"].TotalPower, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Top();
        Assert.Equal(runes >= 7 ? 7 : 4, Convert.ToInt32(Assert.Single(game.Events(),
            e => e.Event.Kind == "DAMAGE_APPLIED").Event.Payload["damage"]));
        if (later)
        {
            Assert.Single(DeadlyFlourishTests.Markers(game.State));
            Assert.Empty(game.State.StackItems);
            await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["ENEMY"]));
            await game.Top();
        }
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        var reward = Assert.Single(game.State.StackItems);
        Assert.NotNull(reward.DelayedDestruction); Assert.Empty(reward.TargetObjectIds);
        Assert.Equal("P1", reward.ControllerId); Assert.Equal(Card, reward.CardNo);
        Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count);
        await game.Top();
        Assert.Equal(2, game.State.PlayerZones["P1"].RuneDeck.Count);
        AssertCalled(game.State, "P1", "P1-CALL-0");
        Assert.Empty(DeadlyFlourishTests.Gold(game.State, "P1"));
    }

    [Fact]
    public async Task AChosenFriendlyUnitAlsoEarnsTheRuneForTheSpellController()
    {
        var game = new Driver(Position());
        await game.Act("P1", Play("FRIEND")); await game.Top();
        Assert.Contains("FRIEND", game.State.PlayerZones["P1"].Graveyard);
        Assert.NotNull(Assert.Single(game.State.StackItems).DelayedDestruction);
        await game.Top(); AssertCalled(game.State, "P1", "P1-CALL-0");
    }

    [Theory]
    [InlineData("base")] [InlineData("battlefield")] [InlineData("missing")]
    [InlineData("count")] [InlineData("mana")] [InlineData("turn")] [InlineData("graveyard")]
    public async Task IllegalTargetsUnpaidManaAndUnavailableTimingRejectWithoutChangingStateOrViews(string branch)
    {
        var initial = Position(); var command = Play();
        if (branch == "base") initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
        if (branch == "battlefield") command = Play("FIELD");
        if (branch == "missing") command = Play("FORGED");
        if (branch == "count") command = command with { TargetObjectIds = [] };
        if (branch == "mana") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(0, 20) } };
        if (branch == "turn") initial = initial with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        if (branch == "graveyard") initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = initial.PlayerZones["P1"].Hand.Where(id => id != "SPELL").ToArray(), Graveyard = ["SPELL"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["SPELL"] = new("P1", "GRAVEYARD") } };
        await VenSignatureFlowSpellTests.Reject(initial, command, branch switch {
            "mana" => ErrorCodes.InsufficientCost, "turn" => ErrorCodes.PhaseNotAllowed,
            "graveyard" => ErrorCodes.CardNotInHand, _ => ErrorCodes.InvalidTarget });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RealRuneCallingOrRecyclingResponsesRecheckTheSevenRuneConditionAtResolution(bool increase)
    {
        var initial = Position(increase ? 6 : 7);
        if (increase) initial = AttackDamageDistributionTests.Spell(initial, "RETURN", "OGN·104/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", Play());
        if (increase)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
            await game.Top();
            AssertCalled(game.State, "P1", "P1-CALL-0");
        }
        else await game.Act("P1", new RecycleRuneCommand("P1-RUNE-0"));
        await game.Top();
        Assert.Equal(increase ? 7 : 4, game.State.CardObjects["ENEMY"].Damage);
        Assert.Single(DeadlyFlourishTests.Markers(game.State));
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 0)] [InlineData(3, 1)] [InlineData(1, 2)]
    public async Task TheDelayedRuneCallUsesTheCurrentDeckAndSharedReplacementWithoutOverdrawing(int available, int wyverns)
    {
        var initial = Position(6, 3, available);
        for (var i = 0; i < wyverns; i++)
            initial = OfficialHoldSequenceTests.AddUnit(initial, "WYVERN-" + i, "RAD·092/167", "FIELD", "P1");
        var game = new Driver(initial);
        await game.Act("P1", Play()); await game.Top();
        Assert.Equal(available, game.State.PlayerZones["P1"].RuneDeck.Count);
        await game.Top();
        var expected = Math.Min(available, 1 + wyverns);
        Assert.Equal(available - expected, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Equal(expected, Convert.ToInt32(Assert.Single(game.Events(), e => e.Event.Kind == "RUNES_CALLED").Event.Payload["count"]));
        for (var i = 0; i < expected; i++) AssertCalled(game.State, "P1", "P1-CALL-" + i);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ReturningTheVictimBeforeOrAfterResolutionDoesNotFollowItsNextIncarnation(bool before)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", Play());
        if (before) await game.Act("P1", new PassPriorityCommand());
        else
        {
            await game.Top();
            await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["ENEMY"]));
            await game.Act("P1", new PassPriorityCommand());
        }
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY"]));
        await game.Top(); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        Assert.Empty(DeadlyFlourishTests.Markers(game.State)); Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "RUNES_CALLED" && Equals(e.Event.Payload["playerId"], "P1"));
    }

    [Fact]
    public async Task ARealTurnEndExpiresTheDelayedRuneInstruction()
    {
        var game = new Driver(Position());
        await game.Act("P1", Play()); await game.Top();
        Assert.Single(DeadlyFlourishTests.Markers(game.State));
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(DeadlyFlourishTests.Markers(game.State));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "RUNES_CALLED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "SPELL"));
    }

    [Fact]
    public async Task IndependentEchoExecutionsEarnTwoRunesFromOneActualDeath()
    {
        var initial = Position(6, 7) with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var game = new Driver(initial);
        await game.Act("P1", Play() with { OptionalCosts = ["ECHO"], RepeatChoices = [new("", ["ENEMY"])] });
        await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        var ordering = Assert.Single(game.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.OrderTriggers);
        var ids = JsonSerializer.SerializeToElement(ordering.Metadata).GetProperty("orderedTriggerIds")
            .EnumerateArray().Select(id => id.GetString()!).ToArray();
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        await game.Top(); await game.Top();
        AssertCalled(game.State, "P1", "P1-CALL-0"); AssertCalled(game.State, "P1", "P1-CALL-1");
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "RUNES_CALLED"));
    }

    [Fact]
    public async Task ARealCounterConsumesTheSpellWithoutDamageOrAnEarnedRune()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "COUNTER", "OGN·045/298", "P2"));
        await game.Act("P1", Play()); var target = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [target])); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Empty(DeadlyFlourishTests.Markers(game.State));
        Assert.Equal(16, game.State.RunePools["P1"].Mana);
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "DAMAGE_APPLIED" or "RUNES_CALLED");
    }

    [Fact]
    public async Task ARealSpellControlChangeUsesTheNewControllersRuneCountAndAwardsTheirRune()
    {
        var game = new Driver(RebuttalRulesTests.Position(Card, Position(7, 3)));
        await game.Act("P1", new PlayCardCommand("R-SPELL", Card, ["ENEMY"]));
        var spell = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("REBUTTAL", RebuttalRulesTests.Card, [spell]));
        await game.Top();
        await game.Act("P2", new PayCostCommand(game.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", ["PAY:ANY"]));
        await game.Act("P2", new PayCostCommand(game.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", ["KEEP_ALL"]));
        await game.Top();
        Assert.Equal(4, Convert.ToInt32(Assert.Single(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED").Event.Payload["damage"]));
        Assert.Equal("P2", Assert.Single(game.State.StackItems).ControllerId);
        await game.Top();
        AssertCalled(game.State, "P2", "P2-CALL-0");
        Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count);
    }

    [Fact]
    public async Task ARealBanishReplacementRemovesTheInstructionWithoutAwardingARune()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(6, 5), "PUNISH", "UNL-007/219", "P1");
        var game = new Driver(initial);
        await game.Act("P1", Play()); await game.Top();
        Assert.Single(DeadlyFlourishTests.Markers(game.State));
        await game.Act("P1", new PlayCardCommand("PUNISH", "UNL-007/219", ["ENEMY"])); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Banished);
        Assert.Empty(DeadlyFlourishTests.Markers(game.State)); Assert.Empty(game.State.StackItems);
        Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count);
    }

    private static void AssertCalled(MatchState state, string player, string id)
    {
        Assert.Contains(id, state.PlayerZones[player].Base);
        Assert.True(state.CardObjects[id].IsExhausted); Assert.Equal("BASE", state.ObjectLocations[id].Zone);
    }

    private static MatchState Position(int runes = 6, int might = 12, int available = 3)
    {
        var initial = VenSignatureFlowSpellTests.Position(Card);
        initial = AttackDamageDistributionTests.Spell(initial, "KILL", "OGN·229/298", "P1");
        var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        var zones = initial.PlayerZones.ToDictionary();
        cards["ENEMY"] = cards["ENEMY"] with { Power = might };
        foreach (var player in initial.Seats.Keys)
        {
            var controlled = Enumerable.Range(0, player == "P1" ? runes : 0).Select(i => player + "-RUNE-" + i).ToArray();
            var deck = Enumerable.Range(0, available).Select(i => player + "-CALL-" + i).ToArray();
            foreach (var id in controlled.Concat(deck))
            {
                cards[id] = new(id, cardNo: "UNL-R01", ownerId: player, controllerId: player,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"]);
                locations[id] = new(player, controlled.Contains(id) ? "BASE" : "RUNE_DECK");
            }
            zones[player] = zones[player] with { Base = zones[player].Base.Concat(controlled).ToArray(), RuneDeck = deck };
        }
        return initial with { RoomId = "SIPHONING-STRIKE", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }
}
