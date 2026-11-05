using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadKaisaCopiedPermissionTests
{
    private const string Kaisa = "RAD·063/167";
    private const string Draw = "VEN·049";

    private static MatchState Position(int songs)
    {
        var state = RadSpellDuelReactionTests.Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·059/221", "P1"),
            ["GLASSES"] = OfficialGarenConquestBattlefieldCountTests.Printed("GLASSES", "VEN·137", "P1"),
            ["COPY"] = OfficialGarenConquestBattlefieldCountTests.Printed("COPY", "OGN·096/298", "P1") } };
        state = LocalDestructionRecallTests.At(state, "GLASSES", "P1", "BASE");
        state = LocalDestructionRecallTests.At(state, "COPY", "P1", "BASE");
        if (songs == 2) state = AttachedRuleTextTests.AddSong(state, "SONG2");
        state = OfficialCopyIdentityTests.AddSpell(state, "DETACH", "SFD·011/221");
        return AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·169/298", "P2");
    }

    private static async Task<Driver> OpenCopiedDuel(int songs)
    {
        var game = new Driver(Position(songs));
        foreach (var gear in songs == 1 ? new[] { "GEAR" } : new[] { "GEAR", "SONG2" })
        {
            await game.Act("P1", new AssembleEquipmentCommand(gear, "HOST", ["ASSEMBLE_GREEN"]));
            await game.Top();
            Assert.Equal(Kaisa, game.State.CardObjects[gear].AttachedRuleText!.CardNo);
        }
        await game.Act("P1", new AssembleEquipmentCommand("GLASSES", "HOST", ["ASSEMBLE_YELLOW"]));
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY"]));
        Assert.Equal("OGN·096/298", game.State.CardObjects["HOST"].CardNo);
        Assert.Equal(Kaisa, game.State.CardObjects["GEAR"].AttachedRuleText!.CardNo);
        await RadSpellDuelReactionTests.OpenDuel(game);
        return game;
    }

    private static bool Offered(Driver game, string source)
        => game.PromptFor("P1").Candidates?.Any(candidate => candidate.Action == CommandTypes.PlayCard
            && candidate.Enabled && candidate.Sources?.Any(item => item.Id == source) == true) == true;

    private static async Task PlayDraw(Driver game, string source)
    {
        Assert.True(Offered(game, source));
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand(source, Draw, []));
        Assert.Equal(mana - 2, game.State.RunePools["P1"].Mana);
        Assert.Single(game.State.StackItems, item => item.SourceObjectId == source);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task RealCopiedTextKeepsReactionPermissionAfterTheHostChangesItsPrintedFace(int songs)
    {
        var game = await OpenCopiedDuel(songs);
        var firstEvent = game.Events().Length;
        await PlayDraw(game, "SPELL");
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.Single(game.Events().Skip(firstEvent), entry => entry.Event.Kind == "CARD_DRAWN");
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("D0", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task DetachingOneCopiedInstanceUpdatesFuturePermissionAndKeepsTheAlreadyPlayedSpell(int songs)
    {
        var game = await OpenCopiedDuel(songs);
        await PlayDraw(game, "SPELL");
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "GEAR"]));
        await game.Top();
        Assert.Null(game.State.CardObjects["GEAR"].AttachedRuleText);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Single(game.State.StackItems, item => item.SourceObjectId == "SPELL");
        if (game.State.PriorityPlayerId != "P1")
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(songs == 2, Offered(game, "FOLLOW"));
        if (songs == 2)
        {
            Assert.Equal(Kaisa, game.State.CardObjects["SONG2"].AttachedRuleText!.CardNo);
            await PlayDraw(game, "FOLLOW");
            await game.Top();
        }
        else await RadSpellDuelReactionTests.Reject(game.State, "P1", new PlayCardCommand("FOLLOW", Draw, []));
        await game.Top();
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheChangedHostEndsTheCopiedPermissionButTheOriginalSpellStillResolves()
    {
        var game = await OpenCopiedDuel(1);
        await PlayDraw(game, "SPELL");
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·169/298", ["HOST"]));
        await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(Kaisa, game.State.CardObjects["HOST"].CardNo);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedRuleText);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Single(game.State.StackItems, item => item.SourceObjectId == "SPELL");
        if (game.State.PriorityPlayerId != "P1")
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.False(Offered(game, "FOLLOW"));
        await RadSpellDuelReactionTests.Reject(game.State, "P1", new PlayCardCommand("FOLLOW", Draw, []));
        await game.Top();
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }
}
