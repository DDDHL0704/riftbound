using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadChronobreakReplayWindowTests
{
    private const string Chronobreak = "RAD·140/167";
    private const string Drake = "VEN·048";
    private const string Unit = "OGN·219/298";
    private const string Reaction = "OGN·048/298";

    private static async Task<Driver> PendingReplay()
    {
        var initial = VenUnitEntryBasicTests.Position(Chronobreak);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", Drake, "P1") } };
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P1", "BASE");
        initial = AttackDamageDistributionTests.Spell(initial, "RESPONSE", Reaction, "P2");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Chronobreak, [], OptionalCosts: ["ADDITIONAL_POWER:2"]));
        await game.Act("P1", new PassPriorityCommand());
        var response = new PlayCardCommand("RESPONSE", Reaction, []);
        Assert.True(new CoreRuleEngine().PreviewPlayCard(game.State, "P2",
            PlayCostPreviewTests.Request(game.State, response)).CanPay);
        await game.Top();
        Assert.Equal(["ALLY", "ENEMY"], game.State.StackItems.Select(item => item.SourceObjectId));
        Assert.All(game.State.StackItems, item => Assert.NotNull(item.DeckPlayPendingCard));
        Assert.Equal("P1", game.State.PendingEffectPlay!.PlayerId);
        Assert.Equal("ALLY", Assert.Single(game.State.PendingEffectPlay.Sources).Key);
        AssertClosedWindow(game);
        Assert.Equal(18, game.State.RunePools["P1"].Mana);
        Assert.Equal(18, game.State.RunePools["P1"].TotalPower);
        return game;
    }

    private static async Task PlayFirst(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("ALLY", Drake, []));
        Assert.Equal("P2", game.State.PendingEffectPlay!.PlayerId);
        Assert.Equal("ENEMY", Assert.Single(game.State.PendingEffectPlay.Sources).Key);
        Assert.Null(game.State.PendingCardChoice);
        AssertClosedWindow(game);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
    }

    private static void AssertClosedWindow(Driver game)
    {
        foreach (var prompt in ResolutionResult.BuildPrompts(game.State).Values)
        {
            Assert.DoesNotContain(CommandTypes.PassPriority, prompt.EnabledActions());
            Assert.DoesNotContain(CommandTypes.PassFocus, prompt.EnabledActions());
        }
    }

    private static async Task Finish(Driver game, bool firstPlayed = false)
    {
        var p1Resources = game.State.RunePools["P1"];
        var p2Resources = game.State.RunePools["P2"];
        if (!firstPlayed) await PlayFirst(game);
        await game.Act("P2", new PlayCardCommand("ENEMY", Unit, []));
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        Assert.Equal("P2", game.State.CardObjects["ENEMY"].ControllerId);
        Assert.Equal(2, game.Events().Count(entry => entry.Event.Kind == "UNIT_PLAYED_TO_BASE"));
        await game.Top();
        Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("RESPONSE", game.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "EFFECT_PLAY_UNABLE");
        Assert.Equal(p1Resources, game.State.RunePools["P1"]);
        Assert.Equal(p2Resources, game.State.RunePools["P2"]);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    private static async Task Reject(Driver game, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(game.State);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State));
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State,
            new("closed-chronobreak-window-" + command.CmdType, player, command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(rejected.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(rejected.State)));
    }

    [Fact]
    public async Task TheOriginalControllerCannotConfirmTheBorrowedOwnersPendingUnit()
    {
        var game = await PendingReplay();
        await PlayFirst(game);
        var command = new PlayCardCommand("ENEMY", Unit, []);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(game.State, "P1",
            PlayCostPreviewTests.Request(game.State, command)).CanPay);
        await Reject(game, "P1", command);
        await Finish(game, firstPlayed: true);
    }

    [Fact]
    public async Task AQueuedLaterUnitCannotBeConfirmedBeforeTheCurrentOwnersFirstUnit()
    {
        var game = await PendingReplay();
        var command = new PlayCardCommand("ENEMY", Unit, []);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(game.State, "P2",
            PlayCostPreviewTests.Request(game.State, command)).CanPay);
        await Reject(game, "P2", command);
        await Finish(game);
    }

    [Fact]
    public async Task ARealReactionThatWasLegalBeforeResolutionCannotInterruptPendingReplay()
    {
        var game = await PendingReplay();
        var command = new PlayCardCommand("RESPONSE", Reaction, []);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(game.State, "P2",
            PlayCostPreviewTests.Request(game.State, command)).CanPay);
        await Reject(game, "P2", command);
        await Finish(game);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PassingPriorityOrFocusCannotSkipTheCurrentPlayableUnit(bool focus)
    {
        var game = await PendingReplay();
        await Reject(game, "P1", focus ? new PassFocusCommand() : new PassPriorityCommand());
        await Finish(game);
    }
}
