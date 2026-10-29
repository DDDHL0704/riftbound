using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenTimeFissureTests
{
    private const string Spell = "VEN·066";

    [Theory]
    [InlineData("P1", false)] [InlineData("P1", true)]
    [InlineData("P2", false)] [InlineData("P2", true)]
    public async Task OwnerReplaysTheNewObjectToExactlyItsResolutionLocationWithBaseCostsWaived(string player, bool battlefield)
    {
        var state = Position(); var target = player == "P1" ? "ALLY" : "ENEMY"; var field = player == "P1" ? "BF" : "OTHER";
        if (battlefield) state = LocalDestructionRecallTests.At(state, target, player, "BATTLEFIELD", field);
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", Spell, [target])); await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(player, pending.PlayerId); Assert.Equal("ORIGINAL_LOCATION", pending.DestinationPolicy);
        Assert.True(pending.IgnoreBaseMana); Assert.True(pending.IgnoreBasePower); Assert.False(pending.IgnoreAllCosts);
        Assert.Contains(target, game.State.PlayerZones[player].Banished); Assert.Equal(1, game.State.CardObjects[target].ObjectGeneration);
        var destination = battlefield ? "BATTLEFIELD:" + field : "BASE";
        var choices = Assert.Single(game.PromptFor(player).Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Equal([destination], choices.Destinations!.Select(d => d.Id));
        var play = new PlayCardCommand(target, "OGN·219/298", [], Destination: destination);
        await Reject(game.State, player, play with { Destination = battlefield ? "BASE" : "BATTLEFIELD:" + field });
        await Reject(game.State, player == "P1" ? "P2" : "P1", play);
        await Reject(game.State, player, new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        var pool = game.State.RunePools[player];
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, player, PlayCostPreviewTests.Request(game.State, play));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act(player, play);
        Assert.Null(game.State.PendingEffectPlay); Assert.Equal(pool, game.State.RunePools[player]);
        Assert.Equal(2, game.State.CardObjects[target].ObjectGeneration);
        Assert.Equal(player, game.State.CardObjects[target].ControllerId);
        Assert.Equal(battlefield ? field : null, game.State.ObjectLocations[target].BattlefieldObjectId);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task MovementInResponseChangesTheSameLocationReferencedAtResolution()
    {
        var state = LocalDestructionRecallTests.At(Position(), "ALLY", "P1", "BATTLEFIELD", "BF");
        state = AttackDamageDistributionTests.Spell(state, "FLASH", "OGS·011/024", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", Spell, ["ALLY"]));
        await game.Act("P1", new PlayCardCommand("FLASH", "OGS·011/024", ["ALLY"])); await game.Top(); await game.Top();
        Assert.Equal("BASE", game.State.PendingEffectPlay!.OriginalLocation!.Zone);
        await game.Act("P1", new PlayCardCommand("ALLY", "OGN·219/298", []));
        Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone); await game.Replay();
    }

    [Fact]
    public async Task ATokenVanishesAfterBanishmentWithoutStartingAnImpossibleReplay()
    {
        var state = Position(); var token = P6TokenFactoryCatalog.GetAll().Single(t => t.CardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["ALLY"] = token.CreateObject("ALLY", "P1", "P1") } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", Spell, ["ALLY"])); await game.Top();
        Assert.Null(game.State.PendingEffectPlay); Assert.False(game.State.CardObjects.ContainsKey("ALLY"));
        Assert.DoesNotContain("ALLY", game.State.PlayerZones["P1"].Banished);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task MandatoryAdditionalSacrificeRemainsRequiredByTheNewPlay()
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "OGN·208/298", "P1"),
            ["VICTIM"] = OfficialGarenConquestBattlefieldCountTests.Printed("VICTIM", "OGN·219/298", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["VICTIM"] = new("P1", "BASE") },
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Base = [..p.Value.Base, "VICTIM"] } : p.Value) };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", Spell, ["ALLY"])); await game.Top();
        var play = new PlayCardCommand("ALLY", "OGN·208/298", []);
        await Reject(game.State, "P1", play);
        await game.Act("P1", play with { OptionalCosts = ["DESTROY_FRIENDLY_UNIT:VICTIM"] });
        Assert.Contains("VICTIM", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Fact]
    public async Task RejectedTargetsAndUnpayableSpellDoNotChangeState()
    {
        var state = Position();
        foreach (var target in new[] { "BF", "GEAR", "GRAVE", "UNIT" })
            await Reject(state, "P1", new PlayCardCommand("UNIT", Spell, [target]));
        await Reject(state with { RunePools = state.RunePools.ToDictionary(p => p.Key, p => RunePool.Empty) },
            "P1", new PlayCardCommand("UNIT", Spell, ["ALLY"]));
    }

    [Fact]
    public async Task RecoveryRejectsChangingTheOriginalBattlefieldEvenWithAllProjectionsRebuilt()
    {
        var initial = LocalDestructionRecallTests.At(Position(), "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", Spell, ["ALLY"])); await game.Top();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var persistedEvents = JsonSerializer.Deserialize<RecoveredEvent[]>(JsonSerializer.Serialize(game.Events(), json), json)!;
        var good = game.State;
        var goodSeq = game.Events()[^1].Sequence;
        var goodSnapshots = ResolutionResult.BuildSnapshots(good); var goodPrompts = ResolutionResult.BuildPrompts(good);
        var goodViews = good.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, good.Tick, goodSeq,
            goodSnapshots[p], good.Tick, goodSeq, goodPrompts[p]));
        var goodSpectator = MatchReplayRedactor.BuildSpectatorFrame(good.RoomId, good.Tick, goodSeq, [], good);
        Assert.Empty(MatchRecoveryValidator.Validate(good.RoomId, goodSeq, game.Commands(), persistedEvents,
            goodViews, good, good.Tick, goodSpectator));
        var bad = game.State with { PendingEffectPlay = game.State.PendingEffectPlay! with {
            OriginalLocation = new("P1", "BATTLEFIELD", "OTHER"), OriginalBattlefield = new("OTHER", 0) } };
        var seq = game.Events()[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(bad); var prompts = ResolutionResult.BuildPrompts(bad);
        var views = bad.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, bad.Tick, seq, snapshots[p], bad.Tick, seq, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(bad.RoomId, bad.Tick, seq, [], bad);
        var errors = MatchRecoveryValidator.Validate(bad.RoomId, seq, game.Commands(), game.Events(), views, bad, bad.Tick, spectator);
        Assert.Contains("original effect-play location differs from accepted banishment", errors);
    }

    private static MatchState Position() => VenUnitEntryBasicTests.Position(Spell);
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-fissure", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
