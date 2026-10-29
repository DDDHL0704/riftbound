using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenEndlessHoardTests
{
    [Fact]
    public async Task PlayingHoardWaitsForResponsesThenBanishesHandAndGraveyardBeforeBurningSeven()
    {
        var initial = Put(VenUnitEntryBasicTests.Position("VEN·022"), "HAND", "VEN·049", "P1", "HAND");
        for (var n = 4; n <= 8; n++) initial = Put(initial, "DECK-" + n, "OGN·219/298", "P1", "MAIN_DECK");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·022", []));
        Assert.Equal(15, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("HAND", game.State.PlayerZones["P1"].Hand); Assert.Contains("GRAVE", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(8, game.State.PlayerZones["P1"].MainDeck.Count);
        await game.Top();
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.All(new[] { "HAND", "GRAVE" }, id => Assert.Contains(id, game.State.PlayerZones["P1"].Banished));
        Assert.Equal(7, game.State.PlayerZones["P1"].Graveyard.Count); Assert.Single(game.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·219/298", "BASE")]
    [InlineData("UNL-085/219", "BASE")]
    [InlineData("VEN·049", "BANISHED")]
    public async Task GraveyardCardsUseNormalTimingAndCostsAndSpellsAreBanishedAfterResolving(string no, string destination)
    {
        var initial = Put(Active(), "PLAY", no, "P1", "GRAVEYARD");
        var command = new PlayCardCommand("PLAY", no, [], OptionalCosts: ["NORMAL_GRAVEYARD_PLAY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        var game = new Driver(initial); await game.Act("P1", command);
        Assert.DoesNotContain("PLAY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(20 - quote.Cost!.Mana, game.State.RunePools["P1"].Mana);
        if (destination == "BANISHED") { Assert.Null(Assert.Single(game.State.StackItems).FlowPlay); await game.Top(); }
        Assert.Equal(destination, game.State.ObjectLocations["PLAY"].Zone); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PrintedAndFlowCostsRemainSeparateChoices(bool normal)
    {
        var initial = Put(Put(Active(), "PLAY", "VEN·003", "P1", "GRAVEYARD"), "OPPOSING_GEAR", "UNL-085/219", "P2", "BASE");
        var command = new PlayCardCommand("PLAY", "VEN·003", ["OPPOSING_GEAR"], OptionalCosts: normal ? ["NORMAL_GRAVEYARD_PLAY"] : []);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(normal ? 2 : 4, quote.Cost!.Mana);
        Assert.Contains("NORMAL_GRAVEYARD_PLAY", JsonSerializer.Serialize(ResolutionResult.BuildPrompts(initial)["P1"]));
        var game = new Driver(initial); await game.Act("P1", command);
        Assert.Equal(!normal, Assert.Single(game.State.StackItems).FlowPlay is not null); await game.Top();
        Assert.Contains("OPPOSING_GEAR", game.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("PLAY", game.State.PlayerZones["P1"].Banished); await game.Replay();
    }

    [Theory]
    [InlineData("no-source")] [InlineData("hidden-source")] [InlineData("enemy-source")]
    [InlineData("enemy-graveyard")] [InlineData("no-resources")] [InlineData("wrong-turn")]
    [InlineData("duplicate-choice")]
    public async Task NormalGraveyardPermissionDoesNotBypassOwnershipTimingOrPayment(string branch)
    {
        var initial = Put(Active(), "PLAY", "OGN·219/298", branch == "enemy-graveyard" ? "P2" : "P1", "GRAVEYARD");
        if (branch == "no-source") initial = Move(initial, "UNIT", "P1", "BANISHED");
        if (branch == "hidden-source") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsFaceDown = true } } };
        if (branch == "enemy-source") initial = Move(initial, "UNIT", "P2", "BASE");
        if (branch == "no-resources") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty } };
        if (branch == "wrong-turn") initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        await Reject(initial, "P1", new PlayCardCommand("PLAY", "OGN·219/298", [],
            OptionalCosts: branch == "duplicate-choice" ? ["NORMAL_GRAVEYARD_PLAY", "NORMAL_GRAVEYARD_PLAY"] : ["NORMAL_GRAVEYARD_PLAY"]));
    }

    [Fact]
    public async Task OnlyTheDrawPhaseIsSkippedWhileEffectDrawsStillWork()
    {
        var initial = Put(Active(), "DRAW", "VEN·049", "P1", "GRAVEYARD");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("DRAW", "VEN·049", [], OptionalCosts: ["NORMAL_GRAVEYARD_PLAY"]));
        await game.Top(); Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Equal(MatchPhases.Main, game.State.Phase); Assert.Equal(2, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Single(game.Events(), e => e.Event.Kind == "DRAW_PHASE_SKIPPED");
        Assert.Equal(new[] { "P1-DRAW-1" }, game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task ReturningHoardRemovesBothGraveyardPermissionAndTheSkippedDrawPhase()
    {
        var initial = Put(Active(), "RETURN", "SFD·135/221", "P1", "HAND");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("RETURN", "SFD·135/221", ["UNIT"]));
        await game.Top(); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("RETURN", game.State.PlayerZones["P1"].Graveyard);
        await Reject(game.State, "P1", new PlayCardCommand("GRAVE", "OGN·219/298", [], OptionalCosts: ["NORMAL_GRAVEYARD_PLAY"]));
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand); Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DRAW_PHASE_SKIPPED");
        await game.Replay();
    }

    [Fact]
    public async Task ACounteredSpellFromTheGraveyardIsBanishedWithoutItsEffect()
    {
        var initial = Put(Put(Active(), "PLAY", "VEN·049", "P1", "GRAVEYARD"), "COUNTER", "OGN·045/298", "P2", "HAND");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("PLAY", "VEN·049", [], OptionalCosts: ["NORMAL_GRAVEYARD_PLAY"]));
        var id = Assert.Single(game.State.StackItems).StackItemId; await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [id])); await game.Top();
        Assert.Contains("PLAY", game.State.PlayerZones["P1"].Banished); Assert.Contains("COUNTER", game.State.PlayerZones["P2"].Graveyard);
        Assert.Empty(game.State.PlayerZones["P1"].Hand); Assert.Equal(3, game.State.PlayerZones["P1"].MainDeck.Count);
        var countered = Assert.Single(game.Events(), e => e.Event.Kind == "STACK_ITEM_COUNTERED");
        Assert.Equal("BANISHED", countered.Event.Payload["destinationZone"]); await game.Replay();
    }

    [Fact]
    public async Task AnIllegalStandbyEntryIsReplacedBeforeItCanEnterTheGraveyard()
    {
        var game = new Driver(Put(Active(), "STANDBY", "OGN·057/298", "P1", "HAND"));
        await game.Act("P1", new HideCardCommand("STANDBY", "OGN·057/298", "BATTLEFIELD:BF", ["STANDBY_A"]));
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        Assert.Contains("STANDBY", game.State.PlayerZones["P1"].Banished);
        Assert.DoesNotContain("STANDBY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("BANISHED", game.State.ObjectLocations["STANDBY"].Zone); await game.Replay();
    }

    [Fact]
    public async Task ReplacedDiscardStillPaysTheCostWithoutDiscardObserversOrDiscardHistory()
    {
        var initial = Put(HandDiscardObserverTests.Position(), "HOARD", "VEN·022", "P1", "BASE");
        var game = new Driver(initial); await game.Act("P1", HandDiscardObserverTests.DiscardCost());
        Assert.Contains("FODDER", game.State.PlayerZones["P1"].Banished);
        Assert.Single(game.State.StackItems); Assert.True(game.State.CardObjects["JINX"].IsExhausted);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "CARD_DISCARDED" or "CARDS_DISCARDED");
        Assert.Single(game.Events(), e => e.Event.Kind == "CARD_BANISHED" && Equals(e.Event.Payload.GetValueOrDefault("replacedAction"), "DISCARD"));
        await game.Top(); Assert.Contains("SPELL", game.State.PlayerZones["P1"].Banished);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard); Assert.True(game.State.CardObjects["JINX"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task SimultaneousSacrificeBanishesTheWholeGroupEvenWhenHoardLeavesFirstAndStillPays()
    {
        var initial = Put(Active(), "BOTTLE", "VEN·067", "P1", "BASE");
        var game = new Driver(initial); await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT", "ALLY", "GEAR"]));
        while (game.State.PendingRuleChoice is { } rule) {
            var selected = rule.Request.Options.FirstOrDefault(o => o.Id == "GRAVEYARD_BANISH:UNIT:UNIT") ?? rule.Request.Options[0];
            await game.Act(rule.Request.PlayerId, new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", [selected.Id]));
        }
        Assert.All(new[] { "UNIT", "ALLY", "GEAR" }, id => Assert.Contains(id, game.State.PlayerZones["P1"].Banished));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "UNIT_DESTROYED" or "EQUIPMENT_DESTROYED");
        Assert.Equal(3, Assert.Single(game.State.StackItems).TriggerCost!.DestroyedPermanents!.Count);
        var score = game.State.PlayerScores["P1"]; await game.Top(); Assert.Equal(score + 1, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task APlayerChoosesBetweenTheBanishAndHourglassRecallReplacements(bool banish)
    {
        var initial = Put(Active(), "HOURGLASS", "OGN·077/298", "P1", "BASE");
        initial = Put(initial, "KILL", "OGN·229/298", "P2", "HAND") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(initial); await game.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", ["ALLY"])); await game.Top();
        var rule = game.State.PendingRuleChoice!;
        await game.Act("P1", new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", [banish ? "GRAVEYARD_BANISH:ALLY:UNIT" : "GEAR:ALLY:HOURGLASS"]));
        Assert.Equal(banish ? "BANISHED" : "BASE", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal(banish ? "BASE" : "BANISHED", game.State.ObjectLocations["HOURGLASS"].Zone); await game.Replay();
    }

    private static MatchState Active() => Move(VenUnitEntryBasicTests.Position("VEN·022"), "UNIT", "P1", "BASE");
    private static MatchState Put(MatchState state, string id, string no, string player, string zone)
        => Move(HandDiscardObserverTests.Put(state, id, no, player, "HAND"), id, player, zone);
    private static MatchState Move(MatchState state, string id, string player, string zone)
    {
        var zones = state.PlayerZones.ToDictionary();
        foreach (var p in zones.Keys.ToArray()) zones[p] = zones[p] with {
            Hand = zones[p].Hand.Where(c => c != id).ToArray(), Base = zones[p].Base.Where(c => c != id).ToArray(),
            MainDeck = zones[p].MainDeck.Where(c => c != id).ToArray(), Graveyard = zones[p].Graveyard.Where(c => c != id).ToArray(),
            Banished = zones[p].Banished.Where(c => c != id).ToArray() };
        zones[player] = zone switch {
            "HAND" => zones[player] with { Hand = [..zones[player].Hand, id] }, "BASE" => zones[player] with { Base = [..zones[player].Base, id] },
            "GRAVEYARD" => zones[player] with { Graveyard = [..zones[player].Graveyard, id] },
            "BANISHED" => zones[player] with { Banished = [..zones[player].Banished, id] },
            _ => zones[player] with { MainDeck = [..zones[player].MainDeck, id] } };
        return state with { PlayerZones = zones, CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = state.CardObjects[id] with { ControllerId = player } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, zone) } };
    }
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-hoard", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
