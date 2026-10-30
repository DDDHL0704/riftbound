using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadHeldInsightDeckPlayUnitTests
{
    private const string Tower = "RAD·067/167";
    private static MatchState DeathPosition(string top = "OGN·219/298", bool empty = false)
    {
        var s = VenUnitEntryBasicTests.Position(Tower);
        s = s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["P1-DRAW-1"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-DRAW-1", top, "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with {
                Hand = [], Base = [..s.PlayerZones["P1"].Base, "UNIT"], MainDeck = empty ? [] : s.PlayerZones["P1"].MainDeck,
                Banished = empty ? s.PlayerZones["P1"].MainDeck : [] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["UNIT"] = new("P1", "BASE") } };
        if (empty) s = s with { ObjectLocations = s.ObjectLocations.ToDictionary(p => p.Key, p => p.Key.StartsWith("P1-DRAW-") ? new ObjectLocationState("P1", "BANISHED") : p.Value) };
        return AttackDamageDistributionTests.Spell(s, "KILL", "OGN·229/298", "P1");
    }

    [Theory]
    [InlineData("OGN·219/298")] [InlineData("SFD·022/221")] [InlineData("OGN·048/298")]
    public async Task LastBreathBanishesExactlyTheTopCardAndCanPlayUnitsEquipmentOrSpellsWithoutMana(string no)
    {
        var game = new Driver(DeathPosition(no));
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["UNIT"])); await game.Top();
        Assert.Null(game.State.PendingEffectPlay); await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.True(pending.IgnoreBaseMana); Assert.False(pending.IgnoreBasePower); Assert.False(pending.IgnoreAllCosts);
        Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Banished);
        Assert.Equal(new[] { "P1-DRAW-2", "P1-DRAW-3" }, game.State.PlayerZones["P1"].MainDeck);
        await Reject(game.State, "P2", new PlayCardCommand("P1-DRAW-1", no, [], Destination: "BASE"));
        await Reject(game.State, "P1", new PlayCardCommand("P1-DRAW-2", "OGN·219/298", [], Destination: "BASE"));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { PendingEffectPlay = pending with { IgnoreBasePower = true } }));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { PendingEffectPlay = pending with {
            Sources = new Dictionary<string, long> { ["P1-DRAW-1"] = pending.Sources["P1-DRAW-1"] + 1 } } }));
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("P1-DRAW-1", no, no == "SFD·022/221" ? ["ALLY"] : [], Destination: "BASE"));
        Assert.Equal(mana, game.State.RunePools["P1"].Mana); Assert.Null(game.State.PendingEffectPlay);
        if (no == "OGN·048/298") await game.Top();
        Assert.DoesNotContain("P1-DRAW-1", game.State.PlayerZones["P1"].Banished);
        Assert.Single(game.Events(), e => e.Event.Kind == "CARD_BANISHED"); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task PrintedPowerStillMustBePaidAndDecliningLeavesTheCardBanished(bool decline)
    {
        var game = new Driver(DeathPosition("RAD·131/167"));
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["UNIT"])); await game.Top(); await game.Top();
        var pending = game.State.PendingEffectPlay!;
        var poor = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { ["P1"] = RunePool.Empty } };
        await Reject(poor, "P1", new PlayCardCommand("P1-DRAW-1", "RAD·131/167", [], Destination: "BASE"));
        var power = game.State.RunePools["P1"].TotalPower; var mana = game.State.RunePools["P1"].Mana;
        if (decline) await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        else await game.Act("P1", new PlayCardCommand("P1-DRAW-1", "RAD·131/167", [], Destination: "BASE"));
        Assert.Null(game.State.PendingEffectPlay); Assert.Empty(game.State.StackItems);
        Assert.Equal(power - (decline ? 0 : 1), game.State.RunePools["P1"].TotalPower);
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(decline, game.State.PlayerZones["P1"].Banished.Contains("P1-DRAW-1")); await game.Replay();
    }

    [Fact]
    public async Task AnEmptyDeckHasNoCardToBanishAndDoesNotDrawOrBurn()
    {
        var game = new Driver(DeathPosition(empty: true));
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["UNIT"])); await game.Top(); await game.Top();
        Assert.Null(game.State.PendingEffectPlay); Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "CARD_BANISHED" or "CARD_DRAWN" or "PLAYER_BURNED"); await game.Replay();
    }

    [Fact]
    public async Task EntryAndHoldingEachOfferThreePrivateInsightCards()
    {
        var game = new Driver(LastBreathDrawBoundaryTests.Add(VenUnitEntryBasicTests.Position(Tower), "FOURTH", "OGN·219/298", "P1", "MAIN_DECK"));
        await Reject(game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) { ["P1"] = RunePool.Empty } },
            "P1", new PlayCardCommand("UNIT", Tower, [], Destination: "BATTLEFIELD:BF"));
        await game.Act("P1", new PlayCardCommand("UNIT", Tower, [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal(12, game.State.RunePools["P1"].Mana); Assert.Equal(18, game.State.RunePools["P1"].TotalPower);
        await game.Top(); await Insight();
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        Assert.Contains(game.State.StackItems, i => i.InsightContext?.Kind == "HELD");
        await game.Top(); await Insight(); await game.Replay();

        async Task Insight()
        {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("INSIGHT", choice.ChoiceWindow); Assert.Equal(3, choice.LegalObjectIds.Count);
            Assert.DoesNotContain(choice.LegalObjectIds[0], JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State)["P2"]));
            await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, "INSIGHT", []));
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, "INSIGHT", []));
            var order = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("INSIGHT_ORDER", order.ChoiceWindow);
            await game.Act("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, order.LegalObjectIds));
        }
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-held-deck-play", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
