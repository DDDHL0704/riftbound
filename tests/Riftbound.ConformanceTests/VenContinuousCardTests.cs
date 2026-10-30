using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenContinuousCardTests
{
    [Theory]
    [InlineData("VEN·018", 4, 1)]
    [InlineData("VEN·076", 3, 0)]
    [InlineData("VEN·097", 3, 0)]
    public async Task EachCardPaysItsPrintedCostAndEntersBeforeItsContinuousEffectsApply(string no, int mana, int red)
    {
        var state = Position(no);
        var command = new PlayCardCommand("UNIT", no, [], Destination: "BASE");
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(red, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        var driver = new Driver(state);
        await driver.Act("P1", command);
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        Assert.Equal(20 - mana, driver.State.RunePools["P1"].Mana);
        Assert.Empty(driver.State.StackItems);
        if (no == "VEN·018") Assert.Equal(5, Might(driver.State, "P1", "ALLY"));
        if (no == "VEN·076") Assert.Contains("强攻", driver.State.CardObjects["UNIT"].Tags);
        if (no == "VEN·097") Assert.Equal(1, Might(driver.State, "P1", "UNIT"));
        await driver.Replay();
    }

    [Theory]
    [InlineData("VEN·018")] [InlineData("VEN·076")] [InlineData("VEN·097")]
    public async Task UnpayableOrInventedPlayTargetsLeaveTheStateUnchanged(string no)
    {
        var state = Position(no);
        await Reject(state, new PlayCardCommand("UNIT", no, ["ALLY"]));
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 0) } };
        await Reject(state, new PlayCardCommand("UNIT", no, []));
    }

    [Theory]
    [InlineData("same", 3)] [InlineData("other-battlefield", 2)]
    [InlineData("enemy", 2)] [InlineData("graveyard", 2)] [InlineData("different-name", 2)]
    public async Task SpiderlingsCountOnlyOtherFriendlyUnitsWithTheirNameAtTheirCurrentLocation(string branch, int expected)
    {
        var state = Position("VEN·097");
        state = Replace(state, "ALLY", "VEN·097", "P1");
        state = Add(state, "SECOND", branch == "different-name" ? "OGN·219/298" : "VEN·097", "P1");
        if (branch == "other-battlefield") state = LocalDestructionRecallTests.At(state, "SECOND", "P1", "BATTLEFIELD", "BF");
        if (branch == "enemy") state = LocalDestructionRecallTests.At(state, "SECOND", "P2", "BASE");
        if (branch == "graveyard") state = ToPrivateZone(state, "SECOND", "GRAVEYARD");
        var driver = new Driver(state);
        await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·097", []));
        Assert.Equal(expected, Might(driver.State, "P1", "UNIT"));
        Assert.Equal(expected, Might(driver.State, "P1", "ALLY"));
        var effect = Assert.Single(driver.State.ContinuousEffects, e => e.TargetObjectId == "UNIT"
            && e.EffectKind == StaticAuraKinds.SourceOtherSameNameUnitCountPower);
        Assert.Equal(expected - 1, effect.PowerDelta);
        await driver.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        Assert.Equal(expected - 1, Might(driver.State, "P1", "UNIT"));
        Assert.Equal(branch == "other-battlefield" ? 2 : 1, Might(driver.State, "P1", "ALLY"));
        await driver.Replay();
    }

    [Fact]
    public async Task AHiddenSpiderDoesNotCountUntilItsFreeReactionPlayOnALaterTurn()
    {
        var state = Replace(Position("VEN·097"), "ALLY", "VEN·097", "P1");
        state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        var driver = new Driver(state);
        await driver.Act("P1", new HideCardCommand("UNIT", "VEN·097", "BATTLEFIELD:BF", ["STANDBY_A"]));
        Assert.True(driver.State.CardObjects["UNIT"].IsFaceDown);
        Assert.Equal(1, Might(driver.State, "P1", "ALLY"));
        await Reject(driver.State, new PlayCardCommand("UNIT", "VEN·097", [], Destination: "BATTLEFIELD:BF"));
        await driver.Act("P1", new EndTurnCommand());
        await driver.Act("P2", new EndTurnCommand());
        var mana = driver.State.RunePools["P1"].Mana;
        await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·097", [], Destination: "BATTLEFIELD:BF"));
        Assert.False(driver.State.CardObjects["UNIT"].IsFaceDown);
        Assert.Equal(mana, driver.State.RunePools["P1"].Mana);
        Assert.Equal(2, Might(driver.State, "P1", "UNIT"));
        Assert.Equal(2, Might(driver.State, "P1", "ALLY"));
        await driver.Replay();
    }

    [Theory]
    [InlineData(4)] [InlineData(50)]
    public async Task AnExplicitUnlimitedCopiesTextOverridesTheOrdinaryThreeCopyLimit(int count)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var spiders = OfficialDeckValidator.Validate(new("", "", Enumerable.Repeat("VEN·097", count).ToArray(), [], []), catalog);
        Assert.DoesNotContain(spiders.Errors, e => e.Contains("《小蜘蛛》") && e.Contains("上限"));
        var ordinary = OfficialDeckValidator.Validate(new("", "", Enumerable.Repeat("OGN·219/298", count).ToArray(), [], []), catalog);
        Assert.Contains(ordinary.Errors, e => e.Contains("上限为 3 张"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public async Task MaintenanceAssaultTracksCurrentPublicEquipmentAndDropsWhenEquipmentLeaves(int count)
    {
        var state = Position("VEN·076");
        state = ToPrivateZone(state, "GEAR", "HAND");
        for (var i = 0; i < count; i++) state = Add(state, "ITEM-" + i, "SFD·022/221", "P1");
        state = Add(state, "OPPONENT-GEAR", "SFD·022/221", "P2");
        state = AttackDamageDistributionTests.Spell(state, "DESTROY", "VEN·003", "P1");
        var driver = new Driver(state);
        await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·076", []));
        Assert.Equal(count, CardCombatKeywordRules.KeywordAmount(driver.State.CardObjects["UNIT"].Tags, CardCombatKeywordNames.Assault));
        Assert.Equal(3, Might(driver.State, "P1", "UNIT"));
        if (count > 0) {
            await driver.Act("P1", new PlayCardCommand("DESTROY", "VEN·003", ["ITEM-0"]));
            await driver.Top();
            Assert.Equal(count - 1, CardCombatKeywordRules.KeywordAmount(driver.State.CardObjects["UNIT"].Tags, CardCombatKeywordNames.Assault));
        }
        await driver.Replay();
    }

    [Fact]
    public async Task AmplifierUsesOneOrTwoMightForAllFriendlyUnitsAndStopsWhenItLeaves()
    {
        var state = AttackDamageDistributionTests.Spell(Position("VEN·018"), "DESTROY", "VEN·003", "P1");
        var driver = new Driver(state);
        await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·018", []));
        Assert.Equal(5, Might(driver.State, "P1", "ALLY"));
        Assert.Equal(4, Might(driver.State, "P2", "ENEMY"));
        await driver.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        Assert.False(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(5, Might(driver.State, "P1", "ALLY"));
        await driver.Top();
        Assert.True(driver.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal(6, Might(driver.State, "P1", "ALLY"));
        Assert.Equal(2, Assert.Single(driver.State.ContinuousEffects, e => e.SourceObjectId == "UNIT"
            && e.TargetObjectId == "ALLY" && e.EffectKind == StaticAuraKinds.FriendlyUnitsPower).PowerDelta);
        await Reject(driver.State, new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        await driver.Act("P1", new PlayCardCommand("DESTROY", "VEN·003", ["UNIT"]));
        await driver.Top();
        Assert.Equal(4, Might(driver.State, "P1", "ALLY"));
        Assert.DoesNotContain(driver.State.ContinuousEffects, e => e.SourceObjectId == "UNIT");
        await driver.Replay();
    }

    [Theory]
    [InlineData("VEN·097", "P1", "powerDelta")]
    [InlineData("VEN·097", "P2", "participantObjectIds")]
    [InlineData("VEN·097", "spectator", "missing")]
    [InlineData("VEN·018", "P1", "powerDelta")]
    [InlineData("VEN·018", "P2", "sourceObjectId")]
    [InlineData("VEN·018", "spectator", "missing")]
    public async Task RecoveryBindsDynamicAuraContributionsToTheCurrentAuthoritativeState(string no, string viewer, string mutation)
    {
        var initial = Position(no);
        if (no == "VEN·097") initial = Replace(initial, "ALLY", no, "P1");
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, []));
        var state = driver.State;
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, 0, snapshots[p], state.Tick, 0, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
        var snapshot = viewer == "spectator" ? spectator.SpectatorSnapshot : snapshots[viewer];
        var rows = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(snapshot.Timing["continuousEffects"]).ToList();
        var row = Assert.Single(rows, r => Equals(r.GetValueOrDefault("sourceObjectId"), "UNIT")
            && Equals(r.GetValueOrDefault("targetObjectId"), no == "VEN·097" ? "UNIT" : "ALLY"));
        if (mutation == "missing") rows.Remove(row);
        else row[mutation] = mutation == "powerDelta" ? 999 : mutation == "participantObjectIds" ? new[] { "ENEMY" } : "GEAR";
        Assert.IsAssignableFrom<IDictionary<string, object?>>(snapshot.Timing)["continuousEffects"] = rows.ToArray();
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
    }

    private static MatchState Position(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Red] = 10 }) } };
    }

    private static MatchState Replace(MatchState state, string id, string no, string player)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player) } };

    private static MatchState ToPrivateZone(MatchState state, string id, string zone)
    {
        var own = state.PlayerZones["P1"];
        return state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = own with { Base = own.Base.Where(x => x != id).ToArray(),
                Hand = zone == "HAND" ? [..own.Hand, id] : own.Hand,
                Graveyard = zone == "GRAVEYARD" ? [..own.Graveyard, id] : own.Graveyard } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", zone) } };
    }

    private static MatchState Add(MatchState state, string id, string no, string player)
        => Replace(state, id, no, player) with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            [player] = state.PlayerZones[player] with { Base = [..state.PlayerZones[player].Base, id] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "BASE") } };

    private static int Might(MatchState state, string player, string id)
        => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[player], new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .GetProperty("players").GetProperty(player).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-continuous", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }
}
