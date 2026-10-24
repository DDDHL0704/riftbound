using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class MinimumMightAuditTests
{
    [Theory]
    [InlineData("OGN·095/298", 1, 0)]
    [InlineData("OGN·093/298", 2, 0)]
    public async Task MinimumSnapshotsCurrentMightIncludingTheLocalAura(string spell, int raw, int expectedRaw)
    {
        var s = CurrentPowerTargetTests.Position(spell, raw, aura: true);
        Assert.Equal(raw + 1, CurrentPowerTargetTests.DisplayPower(s));
        var done = await OfficialSettReplacementTests.Open(s, spell, "D");
        Assert.Equal(expectedRaw, done.State.CardObjects["D"].Power);
        Assert.Equal(1, CurrentPowerTargetTests.DisplayPower(done.State));
        var moved = At(done.State, "D", "P2", "BASE");
        Assert.Equal(0, CurrentPowerTargetTests.DisplayPower(moved));
        Assert.Equal(expectedRaw - raw, moved.CardObjects["D"].UntilEndOfTurnPowerModifier);
        Restore(done.State);
        Restore(moved);
        var modifier = Assert.Single(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(raw + 1, modifier.SnapshotMight);
        Assert.Equal(expectedRaw, modifier.ResultingPower);
        Assert.Empty(PublicErrors(moved));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task AReductionWithMinimumOneCannotIncreaseAlreadyLowerMight(int raw)
    {
        const string spell = "OGN·095/298";
        var s = CurrentPowerTargetTests.Position(spell, raw);
        var done = await OfficialSettReplacementTests.Open(s, spell, "D");
        Assert.Equal(raw, done.State.CardObjects["D"].Power);
        Assert.Equal(0, done.State.CardObjects["D"].UntilEndOfTurnPowerModifier);
        Restore(done.State);
    }

    [Theory]
    [InlineData("OGN·097/298", 2, true, "D")]
    [InlineData("OGN·116/298", 2, true, "")]
    [InlineData("OGN·266/298", 1, false, "BF")]
    public async Task UnitAndBattlefieldConsumersUseCurrentMight(string card, int raw, bool unit, string target)
    {
        var s = CurrentPowerTargetTests.Position(card, raw, aura: true);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["AOE"] = s.CardObjects["AOE"] with { Tags = [unit ? CardObjectTags.UnitCard : CardObjectTags.SpellCard] } } };
        var done = await OfficialSettReplacementTests.Open(s, card, target.Length == 0 ? [] : [target]);
        Assert.Equal(0, done.State.CardObjects["D"].Power);
        Assert.Equal(1, CurrentPowerTargetTests.DisplayPower(done.State));
        Assert.Equal(raw + 1, Assert.Single(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers).SnapshotMight);
        Restore(done.State);
        Assert.Empty(PublicErrors(done.State));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task AuthoritativeRecoveryRejectsMissingOrContradictoryMightProvenance(int? forged)
    {
        var done = await OfficialSettReplacementTests.Open(CurrentPowerTargetTests.Position("OGN·095/298", 1, aura: true), "OGN·095/298", "D");
        var original = done.State.CardObjects["D"];
        var modifier = Assert.Single(original.UntilEndOfTurnPowerModifiers);
        var bad = done.State with { CardObjects = new Dictionary<string, CardObjectState>(done.State.CardObjects) {
            ["D"] = original with { UntilEndOfTurnPowerModifiers = [modifier with { SnapshotMight = forged }] } } };
        Assert.NotEqual(MatchStateHasher.Hash(done.State), MatchStateHasher.Hash(bad));
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(bad), e => e.Contains("snapshot Might", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, null)] [InlineData(false, 0)] [InlineData(false, 100)]
    [InlineData(true, null)] [InlineData(true, 0)] [InlineData(true, 100)]
    public async Task PublicRecoveryRejectsForgedHistoricalMight(bool spectator, int? forged)
    {
        var done = await OfficialSettReplacementTests.Open(CurrentPowerTargetTests.Position("OGN·095/298", 1, aura: true), "OGN·095/298", "D");
        Assert.Empty(PublicErrors(done.State));
        var frame = MatchReplayRedactor.BuildSpectatorFrame(done.State.RoomId, done.State.Tick, 0, [], done.State);
        var snapshot = spectator ? frame.SpectatorSnapshot : ResolutionResult.BuildSnapshots(done.State)["P1"];
        var timing = new Dictionary<string, object?>(snapshot.Timing);
        var effects = Assert.IsAssignableFrom<IEnumerable<object?>>(timing["continuousEffects"]).ToArray();
        var index = Array.FindIndex(effects, e => e is Dictionary<string, object?> d && d.ContainsKey("snapshotMight"));
        Assert.True(index >= 0);
        var effect = new Dictionary<string, object?>(Assert.IsType<Dictionary<string, object?>>(effects[index]));
        if (forged.HasValue) effect["snapshotMight"] = forged.Value;
        else effect.Remove("snapshotMight");
        effects[index] = effect;
        timing["continuousEffects"] = effects;
        var changed = snapshot with { Timing = timing };
        var views = spectator ? new Dictionary<string, RecoveredPlayerView>() : new Dictionary<string, RecoveredPlayerView> {
            ["P1"] = new("P1", done.State.Tick, 0, changed, null, null, null) };
        var errors = MatchRecoveryValidator.Validate(done.State.RoomId, 0, [], [], views, done.State, done.State.Tick,
            spectator ? frame with { SpectatorSnapshot = changed } : null);
        Assert.Contains(errors, e => e.Contains("snapshot Might", StringComparison.Ordinal));
    }

    internal static IReadOnlyList<string> PublicErrors(MatchState s)
    {
        var views = ResolutionResult.BuildSnapshots(s).ToDictionary(p => p.Key,
            p => new RecoveredPlayerView(p.Key, s.Tick, 0, p.Value, null, null, null));
        var frame = MatchReplayRedactor.BuildSpectatorFrame(s.RoomId, s.Tick, 0, [], s);
        return MatchRecoveryValidator.ValidateComponentStateAndViews(s, views, frame);
    }

    [Theory]
    [InlineData("OGN·097/298", true, "D")]
    [InlineData("OGN·116/298", true, "")]
    [InlineData("OGN·266/298", false, "BF")]
    [InlineData("OGN·093/298", false, "D")]
    public async Task OtherConsumersCannotRaiseUnitsBelowTheirMinimum(string card, bool unit, string target)
    {
        var s = CurrentPowerTargetTests.Position(card, -2);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["AOE"] = s.CardObjects["AOE"] with { Tags = [unit ? CardObjectTags.UnitCard : CardObjectTags.SpellCard] } } };
        var done = await OfficialSettReplacementTests.Open(s, card, target.Length == 0 ? [] : [target]);
        Assert.Equal(-2, done.State.CardObjects["D"].Power);
        Assert.Empty(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers);
        Restore(done.State); Assert.Empty(PublicErrors(done.State));
    }

    [Fact]
    public async Task MinimumIncludesActiveBattleRoleMightAndDoesNotPersistAsAnAura()
    {
        const string card = "OGN·093/298";
        var s = CurrentPowerTargetTests.Position(card, 0);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { Tags = [CardObjectTags.UnitCard, "坚守2"] } } };
        s = (await Act(s, "P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []))).State;
        Assert.Equal(2, CurrentPowerTargetTests.DisplayPower(s));
        var done = await Top((await Act(s, "P1", new PlayCardCommand("AOE", card, ["D"]))).State);
        Assert.Equal(-1, done.State.CardObjects["D"].Power);
        Assert.Equal(1, CurrentPowerTargetTests.DisplayPower(done.State));
        Assert.Equal(2, Assert.Single(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers).SnapshotMight);
        Restore(done.State); Assert.Empty(PublicErrors(done.State));
    }

    [Fact]
    public async Task TurnEndRemovesBothTheCapturedReductionAndItsProvenance()
    {
        var s = CurrentPowerTargetTests.Position("OGN·095/298", 1, aura: true);
        var cards = new Dictionary<string, CardObjectState>(s.CardObjects);
        var zones = new Dictionary<string, PlayerZones>(s.PlayerZones);
        var locations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations);
        foreach (var player in new[] { "P1", "P2" }) {
            var id = "DRAW-" + player;
            cards[id] = new(id, cardNo: "OGN·012/298", ownerId: player, controllerId: player, power: 4, tags: [CardObjectTags.UnitCard]);
            zones[player] = zones[player] with { MainDeck = [id] };
            locations[id] = new(player, "MAIN_DECK");
        }
        s = s with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
        var done = await OfficialSettReplacementTests.Open(s, "OGN·095/298", "D");
        var ended = await Act(done.State, "P1", new EndTurnCommand());
        Assert.Equal(1, ended.State.CardObjects["D"].Power);
        Assert.Equal(0, ended.State.CardObjects["D"].UntilEndOfTurnPowerModifier);
        Assert.Empty(ended.State.CardObjects["D"].UntilEndOfTurnPowerModifiers);
        Assert.DoesNotContain(ended.State.ContinuousEffects, e => e.SnapshotMight.HasValue);
        Restore(ended.State); Assert.Empty(PublicErrors(ended.State));
    }
}
