using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadSpinningAxeCopiedKeywordTests
{
    private const string Axe = "RAD·148/167";
    private const string Steadfast = "RAD·031/167";

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position(Axe);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(Steadfast, out var behavior));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", Steadfast, "P1") with {
                Tags = [CardObjectTags.UnitCard, ..behavior.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries)] },
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·082/298", "P2"),
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·059/221", "P1") } };
        state = LastBreathDrawBoundaryTests.Add(state, "SONG2", "SFD·059/221", "P1", "BASE");
        state = LastBreathDrawBoundaryTests.Add(state, "EYE", "VEN·137", "P1", "BASE");
        state = LastBreathDrawBoundaryTests.Add(state, "COPY-SOURCE", "OGN·219/298", "P1", "BASE");
        foreach (var id in new[] { "ATTACH", "ATTACH2", "DETACH", "FACE" })
            state = OfficialCopyIdentityTests.AddSpell(state, id, "SFD·011/221");
        return state;
    }

    private static int Keyword(Driver game)
        => RadCombatKeywordDoubleTests.Keyword(game.State, "ALLY", CardCombatKeywordNames.Steadfast);

    private static async Task Toggle(Driver game, string spell, string equipment)
    {
        await game.Act("P1", new PlayCardCommand(spell, "SFD·011/221", ["ALLY", equipment]));
        await game.Top();
    }

    private static async Task CopyHost(Driver game)
    {
        await Toggle(game, "FACE", "EYE");
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("ATTACHMENT_COPY_SELECTION", choice.Request.Kind);
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY-SOURCE"]));
        Assert.Equal("OGN·219/298", game.State.CardObjects["ALLY"].CardNo);
        // The Song retains the printed Steadfast 3 it copied before this face change.
        Assert.Equal(Steadfast, game.State.CardObjects["GEAR"].AttachedRuleText!.CardNo);
        Assert.Equal(3, Keyword(game));
    }

    private static async Task Cast(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("UNIT", Axe, ["ALLY", "ENEMY"]));
        Assert.DoesNotContain(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects,
            effect => effect.StartsWith("COMBAT_KEYWORD:", StringComparison.Ordinal));
    }

    private static async Task ResolveAndCheck(Driver game, int expected)
    {
        var firstEvent = game.Events().Length;
        await game.Top();
        Assert.Equal(expected, Keyword(game));
        Assert.Contains("COMBAT_KEYWORD:坚守2:1", game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        var events = game.Events().Skip(firstEvent).Select(entry => entry.Event).ToArray();
        var damage = Assert.Single(events, entry => entry.Kind == "DAMAGE_APPLIED");
        Assert.Equal(expected, damage.Payload["damage"]);
        Assert.Equal("UNIT", damage.Payload["sourceObjectId"]);
        Assert.False(damage.Payload.ContainsKey("damageSourceObjectId"));
        var grant = Assert.Single(events, entry => entry.Kind == "STATUS_EFFECT_APPLIED");
        Assert.True(Array.IndexOf(events, grant) < Array.IndexOf(events, damage));
        if (expected >= 8) Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        else Assert.Equal(expected, game.State.CardObjects["ENEMY"].Damage);
        Assert.Empty(game.State.StackItems);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(expected - 2, Keyword(game));
        Assert.DoesNotContain(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects,
            effect => effect.StartsWith("COMBAT_KEYWORD:", StringComparison.Ordinal));
        await game.Replay();
    }

    [Theory]
    [InlineData(1, 8)] [InlineData(2, 11)]
    public async Task RealSongsAddEachPrintedKeywordOnceBeforeTheSpellsSingleTemporaryGrant(int songs, int damage)
    {
        var game = new Driver(Position());
        await Toggle(game, "ATTACH", "GEAR");
        if (songs == 2) await Toggle(game, "ATTACH2", "SONG2");
        Assert.Equal(3 * (songs + 1), Keyword(game));
        await Cast(game);
        Assert.Equal(3 * (songs + 1), Keyword(game));
        await ResolveAndCheck(game, damage);
    }

    [Fact]
    public async Task ARealSongDetachmentResponseRemovesItsKeywordBeforeTheDamageIsCalculated()
    {
        var game = new Driver(Position());
        await Toggle(game, "ATTACH", "GEAR");
        Assert.Equal(6, Keyword(game));
        await Cast(game);
        await Toggle(game, "DETACH", "GEAR");
        Assert.Null(game.State.CardObjects["GEAR"].AttachedRuleText);
        Assert.Equal(3, Keyword(game));
        await ResolveAndCheck(game, 5);
    }

    [Theory]
    [InlineData(false, 5)] [InlineData(true, 8)]
    public async Task ARealSpectaclesResponseRecalculatesTheFaceWhileKeepingTheSongsEarlierKeyword(
        bool restoreFace, int damage)
    {
        var game = new Driver(Position());
        await Toggle(game, "ATTACH", "GEAR");
        if (restoreFace) await CopyHost(game);
        await Cast(game);
        if (restoreFace)
        {
            await Toggle(game, "DETACH", "EYE");
            Assert.Equal(Steadfast, game.State.CardObjects["ALLY"].CardNo);
            Assert.Equal(6, Keyword(game));
        }
        else await CopyHost(game);
        await ResolveAndCheck(game, damage);
    }
}
