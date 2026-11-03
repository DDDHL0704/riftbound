using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadTokenGrantInteractionTests
{
    private const string NextUnit = "SFD·143/221", SandSpell = "SFD·031/221", CopySpell = "UNL-200/219";

    private static MatchState Position(string spell = SandSpell, int multipliers = 0)
    {
        var state = OfficialTokenReplacementTests.Position(multipliers, spell);
        state = state with {
            Tick = 0,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·139/167", "P1") with { IsEmpowered = true } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } };
        return HandDiscardObserverTests.Put(state, "NEXT", NextUnit, "P1", "HAND");
    }

    private static async Task Grant(Driver game)
    {
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.TimeAssassinLegendAbilityId, [], []));
        await game.Top();
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
    }

    private static async Task ChooseBase(Driver game)
    {
        for (var step = 0; game.State.PendingRuleChoice is { Request.Kind: "TOKEN_PLAY_LOCATION" } pending; step++)
        {
            Assert.True(step < 3);
            await game.Act(pending.Request.PlayerId, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["BASE"]));
        }
    }

    private static string[] Markers(CardObjectState card)
        => card.UntilEndOfTurnEffects.Where(effect => effect.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal)).ToArray();

    private static async Task DestroyAtEnd(Driver game, string id, bool token = true)
    {
        await game.Act("P1", new EndTurnCommand());
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(CoreRuleEngine.TimeAssassinEndDestructionKind, skill.EndAbility!.Kind);
        Assert.Equal(id, skill.SourceObjectId);
        await game.Top();
        if (token)
        {
            Assert.False(game.State.CardObjects.ContainsKey(id));
            Assert.False(game.State.ObjectLocations.ContainsKey(id));
        }
        else Assert.Contains(id, game.State.PlayerZones["P1"].Graveyard);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), id));
        await game.Replay();
    }

    [Fact]
    public async Task OnlyTheFirstUnitInOneExpandedTokenBatchConsumesTheGrant()
    {
        var game = new Driver(Position(multipliers: 1));
        await Grant(game);
        await game.Act("P1", new PlayCardCommand("C", SandSpell, []));
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["Z1"]));
        await ChooseBase(game);
        var created = game.Events().Where(entry => entry.Event.Kind == "UNIT_TOKEN_CREATED")
            .Select(entry => (string)entry.Event.Payload["tokenObjectId"]!).ToArray();
        Assert.Equal(2, created.Length);
        Assert.Single(Markers(game.State.CardObjects[created[0]]));
        Assert.Empty(Markers(game.State.CardObjects[created[1]]));
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        Assert.Single(game.Events(), entry => entry.Event.Kind == "TIME_ASSASSIN_UNIT_MARKED_FOR_DESTRUCTION");
        await DestroyAtEnd(game, created[0]);
        Assert.Contains(created[1], game.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task AnOpponentsLastBreathTokenCannotConsumeTheActivePlayersGrant()
    {
        var state = Position("OGN·229/298");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TARGET"] = OfficialGarenConquestBattlefieldCountTests.Printed("TARGET", "UNL-153/219", "P2") } };
        var game = new Driver(state);
        await Grant(game);
        await game.Act("P1", new PlayCardCommand("C", "OGN·229/298", ["TARGET"]));
        await game.Top();
        Assert.Contains("TARGET", game.State.PlayerZones["P2"].Graveyard);
        await game.Top();
        var opponent = Assert.Single(OfficialTokenReplacementTests.Tokens(game.State));
        Assert.Equal("P2", opponent.ControllerId);
        Assert.Empty(Markers(opponent));
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        var before = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("NEXT", NextUnit, []));
        Assert.Equal(before.Mana - 2, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower, game.State.RunePools["P1"].TotalPower);
        Assert.Single(Markers(game.State.CardObjects["NEXT"]));
        await DestroyAtEnd(game, "NEXT", token: false);
        Assert.Contains(opponent.ObjectId, game.State.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task ZiggsEquipmentTokenPreservesTheGrantUntilAUnitTokenIsPlayed()
    {
        var game = new Driver(HandDiscardObserverTests.Put(Position(), "ZIGGS", "RAD·112/167", "P1", "BASE"));
        await Grant(game);
        await game.Act("P1", new ActivateAbilityCommand("ZIGGS", "RAD_112_EXHAUST_PLAY_BOMB", ["BFZ"]));
        await game.Top();
        var bomb = Assert.Single(OfficialTokenReplacementTests.Tokens(game.State));
        Assert.Equal(P6TokenFactoryCatalog.BombTokenCardNo, bomb.TokenFactoryCardNo);
        Assert.DoesNotContain(CardObjectTags.UnitCard, bomb.Tags);
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await game.Act("P1", new PlayCardCommand("C", SandSpell, []));
        await game.Top();
        await ChooseBase(game);
        var unit = Assert.Single(OfficialTokenReplacementTests.Tokens(game.State), card => card.Tags.Contains(CardObjectTags.UnitCard));
        Assert.Single(Markers(unit));
        Assert.Empty(Markers(game.State.CardObjects[bomb.ObjectId]));
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await DestroyAtEnd(game, unit.ObjectId);
        Assert.True(game.State.CardObjects.ContainsKey(bomb.ObjectId));
    }

    [Fact]
    public async Task AnImagesOwnGrantedSkillSurvivesItsLaterCopyEffect()
    {
        var game = new Driver(Position(CopySpell));
        await Grant(game);
        await game.Act("P1", new PlayCardCommand("C", CopySpell, ["TARGET"]));
        await game.Top();
        var image = Assert.Single(OfficialTokenReplacementTests.Tokens(game.State));
        var marker = Assert.Single(Markers(image));
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, image.TokenFactoryCardNo);
        await game.Top();
        var copied = game.State.CardObjects[image.ObjectId];
        Assert.Equal("SFD·068/221", copied.CardNo);
        Assert.Equal(image.ObjectGeneration, copied.ObjectGeneration);
        Assert.Equal(marker, Assert.Single(Markers(copied)));
        await DestroyAtEnd(game, image.ObjectId);
    }

    [Fact]
    public async Task CopyingAMarkedUnitDoesNotCopyItsGrantedEndSkill()
    {
        var game = new Driver(Position(CopySpell));
        await Grant(game);
        await game.Act("P1", new PlayCardCommand("NEXT", NextUnit, []));
        var marker = Assert.Single(Markers(game.State.CardObjects["NEXT"]));
        await game.Act("P1", new PlayCardCommand("C", CopySpell, ["NEXT"]));
        await game.Top();
        await game.Top();
        var image = Assert.Single(OfficialTokenReplacementTests.Tokens(game.State));
        Assert.Equal(NextUnit, image.CardNo);
        Assert.Empty(Markers(image));
        Assert.Equal(marker, Assert.Single(Markers(game.State.CardObjects["NEXT"])));
        await DestroyAtEnd(game, "NEXT", token: false);
        Assert.Contains(image.ObjectId, game.State.PlayerZones["P1"].Base);
    }
}
