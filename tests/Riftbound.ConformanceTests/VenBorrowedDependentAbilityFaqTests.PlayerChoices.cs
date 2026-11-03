using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState BorrowedPlayerChoicePosition(string donor, bool field = false)
    {
        var state = BorrowedBombPosition(donor);
        if (field) state = RadUnitEffectTestFixture.Place(state, "HOST", "P1", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsEmpowered = true },
            ["UNIT"] = state.CardObjects["UNIT"] with { IsEmpowered = false } } };
        return AttackDamageDistributionTests.Spell(state, "FLASH", "OGS·011/024", "P1");
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task BorrowedPlayerTransferRecallsAndTransfersOnlyTheBorrower(string chosen)
    {
        var initial = BorrowedPlayerChoicePosition("VEN·133", true);
        var id = TemplateId(initial, "获得此牌控制权");
        foreach (var target in new[] { "HOST", "UNIT", "MISSING" })
            await RadGearTestPosition.Reject(initial, "P1", new ActivateAbilityCommand("HOST", id, [target]));
        await RadGearTestPosition.Reject(initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsEmpowered = false },
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsEmpowered = true } } },
            "P1", new ActivateAbilityCommand("HOST", id, [chosen]));
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, [chosen]));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.Empty(Assert.Single(game.State.StackItems).TargetGenerations!);
        await game.Top();
        Assert.Equal("P1", game.State.CardObjects["HOST"].OwnerId);
        Assert.Equal(chosen, game.State.CardObjects["HOST"].ControllerId);
        Assert.Contains("HOST", game.State.PlayerZones[chosen].Base);
        Assert.Equal("P1", game.State.CardObjects["UNIT"].ControllerId);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowedPlayerTransferRechecksTheSourceIdentityAfterAResponse(bool flash)
    {
        var game = new Driver(BorrowedPlayerChoicePosition("VEN·133", true));
        var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(game.State, "获得此牌控制权"), ["P2"]));
        await game.Act("P1", new PlayCardCommand(flash ? "FLASH" : "RETURN", flash ? "OGS·011/024" : "OGN·104/298", ["HOST"]));
        await game.Top();
        await game.Top();
        Assert.Equal(flash ? "P2" : "P1", game.State.CardObjects["HOST"].ControllerId);
        Assert.Equal(flash, generation == game.State.CardObjects["HOST"].ObjectGeneration);
        Assert.Contains("HOST", flash ? game.State.PlayerZones["P2"].Base : game.State.PlayerZones["P1"].Hand);
        Assert.Equal(flash, game.Events().Any(e => e.Event.Kind == "CONTROL_CHANGED"));
        await game.Replay();
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("base")]
    [InlineData("source-return")]
    [InlineData("no-unit")]
    public async Task BorrowedOpponentMovementUsesTheBorrowersBattlefieldAndTheOpponentsDecision(string boundary)
    {
        var initial = BorrowedPlayerChoicePosition("RAD·113/167", boundary != "base");
        initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BASE");
        if (boundary == "no-unit") initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "GRAVEYARD");
        var id = TemplateId(initial, "对手，让其");
        foreach (var target in new[] { "P1", "HOST", "ENEMY", "MISSING" })
            await RadGearTestPosition.Reject(initial, "P1", new ActivateAbilityCommand("HOST", id, [target]));
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["P2"]));
        if (boundary == "source-return")
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
            await game.Top();
        }
        await game.Top();
        if (boundary == "normal")
        {
            var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            Assert.Equal("P2", choice.Request.PlayerId);
            Assert.Equal(["MOVE:ENEMY"], choice.Request.Options.Select(o => o.Id).ToArray());
            await RadGearTestPosition.Reject(game.State, "P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["MOVE:ENEMY"]));
            await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["MOVE:HOST"]));
            await game.Act("P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["MOVE:ENEMY"]));
            Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        }
        else
        {
            Assert.Null(game.State.PendingRuleChoice);
            Assert.Equal(boundary == "no-unit" ? "GRAVEYARD" : "BASE", game.State.ObjectLocations["ENEMY"].Zone);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("UNIT", false, false)]
    [InlineData("SPELL", false, false)]
    [InlineData("SPELL", true, false)]
    [InlineData("SPELL", false, true)]
    public async Task BorrowedOpponentGuessesBeforeTheBorrowersDeckTopIsRevealed(string guess, bool empty, bool leave)
    {
        var initial = BorrowedPlayerChoicePosition("RAD·064/167");
        var top = initial.PlayerZones["P1"].MainDeck.First();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [top] = OfficialGarenConquestBattlefieldCountTests.Printed(top, "OGN·219/298", "P1") } };
        if (empty) foreach (var card in initial.PlayerZones["P1"].MainDeck.ToArray())
            initial = RadUnitEffectTestFixture.Place(initial, card, "P1", "GRAVEYARD");
        var id = TemplateId(initial, "对手猜测");
        foreach (var target in new[] { "P1", "HOST", "MISSING" })
            await RadGearTestPosition.Reject(initial, "P1", new ActivateAbilityCommand("HOST", id, [target]));
        await RadGearTestPosition.Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(0, 20) } },
            "P1", new ActivateAbilityCommand("HOST", id, ["P2"]));
        var game = new Driver(initial);
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["P2"]));
        Assert.Equal(mana - 1, game.State.RunePools["P1"].Mana);
        var captured = Assert.Single(game.State.StackItems);
        foreach (var forged in new[] { captured with { TargetObjectIds = ["P1"] },
            captured with { TargetGenerations = new Dictionary<string, long> { ["P2"] = 0 } } })
            Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        if (leave)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
            await game.Top();
        }
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("P2", choice.Request.PlayerId);
        Assert.Equal("DECK_TOP_TYPE_GUESS", choice.Request.Kind);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "MAIN_DECK_CARDS_REVEALED");
        if (!empty)
            foreach (var snapshot in ResolutionResult.BuildSnapshots(game.State).Values.Append(ResolutionResult.BuildSpectatorSnapshot(game.State)))
                Assert.DoesNotContain(top, JsonSerializer.Serialize(snapshot));
        await RadGearTestPosition.Reject(game.State, "P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [guess]));
        await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["LEGEND"]));
        await game.Act("P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [guess]));
        Assert.Equal(!empty && guess != "UNIT", game.State.PlayerZones["P1"].Hand.Contains(top));
        if (!empty && guess == "UNIT") Assert.Equal(top, game.State.PlayerZones["P1"].MainDeck.Last());
        if (empty) Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "MAIN_DECK_CARDS_REVEALED" or "BURNOUT_APPLIED");
        if (leave) Assert.Equal("HIDDEN", game.Events().Last(e => e.Event.Kind == "CARD_TYPE_GUESSED").Event.Payload["sourceObjectId"]);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BorrowedOpponentGuessKeepsTheSharedObservationBanishAndReplayPermission(bool matched, bool banish)
    {
        var initial = BorrowedPlayerChoicePosition("RAD·064/167");
        var top = initial.PlayerZones["P1"].MainDeck.First();
        var second = initial.PlayerZones["P1"].MainDeck[1];
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [top] = OfficialGarenConquestBattlefieldCountTests.Printed(top, DeckObservationPlayTests.Nocturne, "P1") } };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(initial, "对手猜测"), ["P2"]));
        await game.Top();
        var guess = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        await game.Act("P2", new PayCostCommand(guess.Request.Id, "RULE_REPLACEMENT", [matched ? "UNIT" : "SPELL"]));
        var observation = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("DECK_OBSERVATION_BANISH", observation.Request.Kind);
        Assert.Equal("P1", observation.Request.PlayerId);
        await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(observation.Request.Id, "RULE_REPLACEMENT", ["BANISH"]));
        await game.Act("P1", new PayCostCommand(observation.Request.Id, "RULE_REPLACEMENT", [banish ? "BANISH" : "KEEP"]));
        if (banish)
        {
            var play = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
            await game.Act("P1", new ChooseCardsCommand(play.ChoiceId, "EFFECT_PLAY", []));
            Assert.Contains(top, game.State.PlayerZones["P1"].Banished);
        }
        Assert.Equal(!matched && !banish, game.State.PlayerZones["P1"].Hand.Contains(top));
        Assert.DoesNotContain(second, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(second, game.State.PlayerZones["P1"].MainDeck.First());
        await game.Replay();
    }
}
