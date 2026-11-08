using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class LilliaTokenPlayTriggerTests
{
    private static MatchState Position(string source, string legend = "UNL-189/219")
    {
        var state = VenConditionalPlayCostTests.Place(VenUnitEntryBasicTests.Position(source), "UNIT", source, "P1", "BASE");
        var cards = state.CardObjects.ToDictionary();
        cards["UNIT"] = cards["UNIT"] with { IsExhausted = true };
        Assert.True(PrintedCardFactory.TryCreate("LEGEND", "P1", legend, out var printed));
        cards["LEGEND"] = printed;
        return state with { CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
    }

    private static async Task PlayFaerie(Driver game, int mana = 4)
    {
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.LilliaLegendAbilityId, [], [$"SPEND_MANA:{mana}"]));
        Assert.DoesNotContain(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.FaerieTokenCardNo);
        await game.Top();
        Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.FaerieTokenCardNo);
    }

    internal static async Task Drain(Driver game)
    {
        for (var steps = 0; game.State.StackItems.Count > 0 || game.State.TriggerQueue.Count > 0; steps++)
        {
            Assert.True(steps < 12);
            if (game.State.TriggerQueue.Count > 0 && game.State.PriorityPlayerId is null)
                await game.Act(game.State.ActivePlayerId, new OrderTriggersCommand(OrderedTriggerIds:
                    game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else await game.Top();
        }
    }

    [Theory, InlineData("UNL-058/219"), InlineData("UNL-058a/219")]
    public async Task EachLilliaPrintingEarnsAnIndependentMightSkillForAPlayedFaerieAndItExpires(string no)
    {
        var game = new Driver(Position(no));
        await PlayFaerie(game);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("UNIT", skill.SourceObjectId);
        Assert.Equal("FRIENDLY_UNIT_PLAYED", skill.UnitEvent!.Timing);
        Assert.Empty(skill.TargetObjectIds);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        await game.Top();
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory, InlineData("UNL-058/219"), InlineData("UNL-058a/219")]
    public async Task APreventedBattlefieldTokenPlayDoesNotTriggerLillia(string no)
    {
        var initial = VenConditionalPlayCostTests.Place(Position(no), "WARDEN", "OGN·070/298", "P2", "BATTLEFIELD", "OTHER");
        initial = VenConditionalPlayCostTests.Place(initial, "TOKENS", "RAD·007/167", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("TOKENS", "RAD·007/167", ["OTHER"]));
        await game.Top();
        Assert.DoesNotContain(game.State.CardObjects.Values, TokenObjectRules.IsToken);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "UNIT_PLAY_PREVENTED");
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory, InlineData("UNL-058/219"), InlineData("UNL-058a/219")]
    public async Task EveryUnitTokenInOneInstructionEarnsItsOwnAdditiveSkill(string no)
    {
        var initial = VenConditionalPlayCostTests.Place(Position(no), "BURST", "UNL-069/219", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("BURST", "UNL-069/219", []));
        await game.Top();
        var tokens = game.State.CardObjects.Values.Where(TokenObjectRules.IsToken).ToArray();
        Assert.Equal(2, tokens.Length);
        Assert.All(tokens, token => { Assert.Equal(3, token.Power); Assert.Contains(CardObjectTags.Ephemeral, token.Tags); });
        Assert.Equal(2, game.State.TriggerQueue.Count(t => t.UnitEvent?.Timing == "FRIENDLY_UNIT_PLAYED")
            + game.State.StackItems.Count(t => t.UnitEvent?.Timing == "FRIENDLY_UNIT_PLAYED"));
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        await Drain(game);
        Assert.Equal(6, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "UNIT_POWER_CHANGED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "UNIT")));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory, InlineData("UNL-058/219"), InlineData("UNL-058a/219")]
    public async Task AnActualImageOfLilliaObservesOnlySubsequentTokenPlays(string no)
    {
        var initial = VenConditionalPlayCostTests.Place(Position(no), "UNIT", no, "P1", "BANISHED");
        initial = VenConditionalPlayCostTests.Place(initial, "ENEMY", no, "P2", "BASE");
        initial = AttackDamageDistributionTests.Spell(initial, "COPY", "UNL-200/219", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["ENEMY"]));
        await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal(no, image.CardNo);
        Assert.Equal(4, image.Power);
        // CN383.2: Image gained this text after its own entry. It does not
        // retroactively observe that entry, but does observe the next token.
        Assert.Empty(game.State.StackItems);
        await PlayFaerie(game, 3); // the copied Image is an Ephemeral unit
        Assert.Equal(4, game.State.CardObjects[image.ObjectId].Power);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(image.ObjectId, skill.SourceObjectId);
        Assert.Equal("P1", skill.ControllerId);
        await game.Top();
        Assert.Equal(5, game.State.CardObjects[image.ObjectId].Power);
        Assert.Equal(4, game.State.CardObjects["ENEMY"].Power);
        await game.Replay();
    }

    [Theory, InlineData("UNL-058/219"), InlineData("UNL-058a/219")]
    public async Task TheCurrentControllerObservesItsTokenPlayEvenWhenTheSourceHasAnotherOwner(string no)
    {
        // Declared controller/owner boundary; no unexecuted control-changing
        // spell is inserted into accepted history.
        var initial = Position(no);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { OwnerId = "P2", ControllerId = "P1" } } };
        var game = new Driver(initial);
        await PlayFaerie(game);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("P1", skill.ControllerId);
        Assert.Equal("UNIT", skill.SourceObjectId);
        Assert.Equal("P2", game.State.CardObjects["UNIT"].OwnerId);
        await game.Top();
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.Equal("P2", game.State.CardObjects["UNIT"].OwnerId);
        await game.Replay();
    }

    [Theory, InlineData("unit"), InlineData("equipment-token")]
    public async Task OrdinaryUnitsAndEquipmentTokensDoNotTriggerLillia(string kind)
    {
        var no = kind == "unit" ? "OGN·219/298" : "SFD·004/221";
        var initial = VenConditionalPlayCostTests.Place(Position("UNL-058/219"), "PLAY", no, "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("PLAY", no, []));
        if (kind != "unit") await game.Top();
        Assert.DoesNotContain(game.State.StackItems, i => i.UnitEvent is not null);
        Assert.DoesNotContain(game.State.TriggerQueue, i => i.UnitEvent is not null);
        Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory, InlineData("UNL-058/219", "hidden"), InlineData("UNL-058/219", "graveyard"), InlineData("UNL-058/219", "opponent")]
    [InlineData("UNL-058a/219", "hidden"), InlineData("UNL-058a/219", "graveyard"), InlineData("UNL-058a/219", "opponent")]
    public async Task HiddenOffFieldAndEnemyObserversDoNotEarnTheFriendlyPlayTrigger(string source, string boundary)
    {
        var initial = Position(source);
        if (boundary != "hidden") initial = VenConditionalPlayCostTests.Place(initial, "UNIT", source,
            boundary == "opponent" ? "P2" : "P1", boundary == "opponent" ? "BASE" : "GRAVEYARD");
        else initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsFaceDown = true } } };
        var game = new Driver(initial);
        await PlayFaerie(game);
        Assert.DoesNotContain(game.State.StackItems, i => i.SourceObjectId == "UNIT");
        Assert.DoesNotContain(game.State.TriggerQueue, i => i.SourceObjectId == "UNIT");
        await game.Replay();
    }

    [Theory, InlineData("UNL-058/219"), InlineData("UNL-058a/219")]
    public async Task ARealReturnResponsePreservesTheSkillButDoesNotModifyItsSourceInHand(string no)
    {
        var initial = VenConditionalPlayCostTests.Place(Position(no), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await PlayFaerie(game);
        var skill = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(skill.StackItemId, Assert.Single(game.State.StackItems).StackItemId);
        var source = game.State.CardObjects["UNIT"];
        await game.Top();
        Assert.Equal(source.Power, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(source.IsExhausted, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }
    [Theory, InlineData("UNL-058/219"), InlineData("UNL-058a/219")]
    public async Task CapturedSkillsCannotModifyANewIncarnationAndRecoveryRejectsForgedFrames(string no)
    {
        var initial = Position(no);
        var game = new Driver(initial);
        await PlayFaerie(game);
        var original = game.State;
        var skill = Assert.Single(original.StackItems);
        OfficialInsightAndSpellLockTests.Restore(original);
        foreach (var context in new[] {
            skill.UnitEvent! with { SourceGeneration = skill.UnitEvent!.SourceGeneration + 1 },
            skill.UnitEvent! with { CapturedTick = original.Tick + 1 },
            skill.UnitEvent! with { Timing = "ATTACK" } })
        {
            var forged = original with { StackItems = [skill with { UnitEvent = context }] };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        }
        // Direct object-boundary fixture: a different field incarnation must
        // not inherit an earlier skill. This does not claim a reentry command.
        var changed = original with { CardObjects = new Dictionary<string, CardObjectState>(original.CardObjects) {
            ["UNIT"] = original.CardObjects["UNIT"] with { ObjectGeneration = original.CardObjects["UNIT"].ObjectGeneration + 1 } } };
        var resolved = await OfficialGraveyardRecastTests.Top(changed);
        Assert.Equal(original.CardObjects["UNIT"].Power, resolved.State.CardObjects["UNIT"].Power);
        Assert.Equal(original.CardObjects["UNIT"].IsExhausted, resolved.State.CardObjects["UNIT"].IsExhausted);
        OfficialInsightAndSpellLockTests.Restore(resolved.State);
    }

}
