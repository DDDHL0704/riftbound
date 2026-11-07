using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ConditionalHeroExperienceTests
{
    [Theory]
    [InlineData("UNL-116/219", 4, false)] [InlineData("UNL-116a/219", 4, false)]
    [InlineData("UNL-116/219", 5, true)] [InlineData("UNL-116a/219", 5, true)]
    [InlineData("UNL-116/219", 7, true)] [InlineData("UNL-116a/219", 7, true)]
    public async Task PoppyChecksTheOpponentsDistanceAtEntryAndReadiesWithExperienceAfterResponses(
        string no, int opponentScore, bool triggers)
    {
        var initial = VenUnitEntryBasicTests.Position(no) with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = opponentScore } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P1"));
        if (triggers)
        {
            var skill = Assert.Single(game.State.StackItems);
            Assert.Equal("UNIT", skill.SourceObjectId); Assert.Empty(skill.TargetObjectIds);
            await game.Top();
            Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
            Assert.Equal(3, game.State.PlayerExperience["P1"]);
        }
        else Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory, InlineData("UNL-116/219"), InlineData("UNL-116a/219")]
    public async Task PoppyStillGainsIndependentExperienceWhenAnEnemyFieldRulePreventsReadying(string no)
    {
        var initial = VenUnitEntryBasicTests.Position(no) with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 5 } };
        initial = VenConditionalPlayCostTests.Place(initial, "WARDEN", "OGN·070/298", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(3, game.State.PlayerExperience["P1"]);
        await game.Replay();
    }

    [Theory, InlineData("UNL-116/219"), InlineData("UNL-116a/219")]
    public async Task PoppysIndependentExperienceSurvivesARealSourceReturnWithoutReadyingItsHandIncarnation(string no)
    {
        var initial = VenUnitEntryBasicTests.Position(no) with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 5 } };
        initial = VenConditionalPlayCostTests.Place(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        var returned = game.State.CardObjects["UNIT"];
        await game.Top();
        Assert.Equal(3, game.State.PlayerExperience["P1"]);
        Assert.Equal("HIDDEN", Assert.Single(game.Events(), e => e.Event.Kind == "EXPERIENCE_GAINED")
            .Event.Payload["sourceObjectId"]);
        Assert.Equal(MatchStateHasher.HashValue(returned), MatchStateHasher.HashValue(game.State.CardObjects["UNIT"]));
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-116/219", 5, false)] [InlineData("UNL-116a/219", 5, false)]
    [InlineData("UNL-116/219", 6, true)] [InlineData("UNL-116a/219", 6, true)]
    public async Task PoppyUsesTheBattlefieldsCurrentWinningScoreInsteadOfAHardcodedFivePointThreshold(
        string no, int opponentScore, bool triggers)
    {
        var initial = VenUnitEntryBasicTests.Position(no);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["BF"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF", "OGN·276/298", "P1") },
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = opponentScore } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(triggers ? 1 : 0, game.State.StackItems.Count);
        if (triggers) await game.Top();
        Assert.Equal(!triggers, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(triggers ? 3 : 0, game.State.PlayerExperience.GetValueOrDefault("P1"));
        await game.Replay();
    }

    [Theory, InlineData("UNL-116/219"), InlineData("UNL-116a/219")]
    public async Task PoppysExperienceIsPermanentAndGoesToThePlayingControllerRatherThanItsOwner(string no)
    {
        // Declared Tick-0 ownership boundary; subsequent commands and their
        // recovery/replay journal remain untouched.
        var initial = VenUnitEntryBasicTests.Position(no);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["UNIT"] = initial.CardObjects["UNIT"] with { OwnerId = "P2" } },
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 5 } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
        await game.Top();
        Assert.Equal(3, game.State.PlayerExperience["P1"]);
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P2"));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(3, game.State.PlayerExperience["P1"]);
        await game.Replay();
    }

    private static MatchState KhaBoard(string no, bool defending, bool isolated)
    {
        var initial = AttackDamageDistributionTests.Position("OGN·001/298");
        foreach (var id in new[] { "ENEMY2", "ENEMY3" })
            initial = VenConditionalPlayCostTests.Place(initial, id, "OGN·001/298", "P2", "BASE");
        var source = defending ? "ENEMY1" : "HOST";
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [source] = OfficialGarenConquestBattlefieldCountTests.Printed(source, no, defending ? "P2" : "P1") } };
        if (defending)
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", "OGN·001/298", "P1") } };
        if (!isolated)
            initial = VenConditionalPlayCostTests.Place(initial, "COMPANION", "OGN·001/298", defending ? "P1" : "P2",
                defending ? "BASE" : "BATTLEFIELD", defending ? null : "F");
        return initial;
    }

    [Theory]
    [InlineData("UNL-143/219", false, true)] [InlineData("UNL-143a/219", false, true)]
    [InlineData("UNL-143/219", true, true)] [InlineData("UNL-143a/219", true, true)]
    [InlineData("UNL-143/219", false, false)] [InlineData("UNL-143a/219", false, false)]
    [InlineData("UNL-143/219", true, false)] [InlineData("UNL-143a/219", true, false)]
    public async Task KhazixCapturesAnIsolatedEnemyInEitherBattleRoleThenResolvesMightAndExperience(
        string no, bool defending, bool isolated)
    {
        var initial = KhaBoard(no, defending, isolated);
        var game = new Driver(initial);
        var source = defending ? "ENEMY1" : "HOST"; var player = defending ? "P2" : "P1";
        var movers = defending && !isolated ? new[] { "HOST", "COMPANION" } : new[] { "HOST" };
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", [], movers));
        Assert.Equal(4, game.State.CardObjects[source].Power);
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault(player));
        if (isolated)
        {
            var skill = Assert.Single(game.State.StackItems);
            Assert.Equal(source, skill.SourceObjectId); Assert.Equal(player, skill.ControllerId);
            Assert.Empty(skill.TargetObjectIds);
            await game.Top();
            Assert.Equal(6, game.State.CardObjects[source].Power);
            Assert.Equal(2, game.State.PlayerExperience[player]);
        }
        else
        {
            Assert.DoesNotContain(game.State.StackItems, item => item.SourceObjectId == source && item.UnitEvent is not null);
            Assert.DoesNotContain(game.State.TriggerQueue, item => item.SourceObjectId == source && item.UnitEvent is not null);
        }
        await game.Replay();
    }

    [Theory, InlineData("UNL-143/219"), InlineData("UNL-143a/219")]
    public async Task KhazixKeepsEarnedExperienceAfterARealReturnAndDoesNotModifyTheNewHandObject(string no)
    {
        var initial = VenConditionalPlayCostTests.Place(KhaBoard(no, false, true), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Single(game.State.StackItems);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        var returned = game.State.CardObjects["HOST"];
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Equal(2, game.State.PlayerExperience["P1"]);
        Assert.Equal("HIDDEN", Assert.Single(game.Events(), e => e.Event.Kind == "EXPERIENCE_GAINED")
            .Event.Payload["sourceObjectId"]);
        Assert.Equal(MatchStateHasher.HashValue(returned), MatchStateHasher.HashValue(game.State.CardObjects["HOST"]));
        await game.Replay();
    }

    [Theory, InlineData("UNL-143/219"), InlineData("UNL-143a/219")]
    public async Task KhazixDoesNotRecheckIsolationAfterAResponseAndOnlyTheMightExpiresAtTurnEnd(string no)
    {
        var initial = VenConditionalPlayCostTests.Place(KhaBoard(no, false, true), "RETURN", "OGN·104/298", "P2", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Single(game.State.StackItems);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY1"]));
        await game.Top();
        Assert.Contains("ENEMY1", game.State.PlayerZones["P2"].Hand);
        await game.Top();
        Assert.Equal(6, game.State.CardObjects["HOST"].Power);
        Assert.Equal(2, game.State.PlayerExperience["P1"]);
        for (var i = 0; game.State.FocusPlayerId is { } focus && i < 6; i++)
            await game.Act(focus, new PassFocusCommand());
        Assert.False(game.State.BattleState.IsActive);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(4, game.State.CardObjects["HOST"].Power);
        Assert.Equal(2, game.State.PlayerExperience["P1"]);
        await game.Replay();
    }

    [Theory, InlineData("UNL-143/219"), InlineData("UNL-143a/219")]
    public async Task KhazixUsesEnemyControlForIsolationAndTheSkillControllerForExperience(string no)
    {
        // Declared Tick-0 mixed-ownership boundary. Both controllers and all
        // field/zone locations still agree; ownership is deliberately opposite.
        var initial = KhaBoard(no, false, true);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { OwnerId = "P2" },
            ["ENEMY1"] = initial.CardObjects["ENEMY1"] with { OwnerId = "P1" } } };
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
        await game.Top();
        Assert.Equal(6, game.State.CardObjects["HOST"].Power);
        Assert.Equal(2, game.State.PlayerExperience["P1"]);
        Assert.Equal(0, game.State.PlayerExperience.GetValueOrDefault("P2"));
        await game.Replay();
    }
}
