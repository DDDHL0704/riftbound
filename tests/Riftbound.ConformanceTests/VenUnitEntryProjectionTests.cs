using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenUnitEntryProjectionTests
{
    [Theory]
    [InlineData("VEN·026")]
    [InlineData("VEN·048")]
    [InlineData("OGN·087/298")]
    public async Task AReturnedOrdinaryPlaySkillKeepsItsPublicFaceWithoutIdentifyingTheNewPrivateSource(string no)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(VenUnitEntryBasicTests.Position(no), "RETURN", "OGN·104/298");
        var driver = new Driver(initial); await Enter(driver, no);
        var oldGeneration = driver.State.CardObjects["UNIT"].ObjectGeneration;
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand);
        Assert.True(driver.State.CardObjects["UNIT"].ObjectGeneration > oldGeneration);
        var snapshots = ResolutionResult.BuildSnapshots(driver.State);
        AssertFace(snapshots["P1"], no, "UNIT"); AssertFace(snapshots["P2"], no, "HIDDEN");
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(driver.State.RoomId, driver.State.Tick, driver.Events().Length, [], driver.State);
        AssertFace(spectator.SpectatorSnapshot, no, "HIDDEN");
        Assert.DoesNotContain("P1-DRAW-1", JsonSerializer.Serialize(snapshots["P2"]));
        Assert.DoesNotContain("P1-DRAW-1", JsonSerializer.Serialize(spectator.SpectatorSnapshot));
        Assert.DoesNotContain("UNIT", Strings(JsonSerializer.SerializeToElement(ResolutionResult.BuildPrompts(driver.State)["P2"])));

        var sequence = driver.Events().Length; var prompts = ResolutionResult.BuildPrompts(driver.State);
        var views = driver.State.Seats.Keys.ToDictionary(p => p,
            p => new RecoveredPlayerView(p, driver.State.Tick, sequence, snapshots[p], driver.State.Tick, sequence, prompts[p]));
        Assert.Empty(MatchRecoveryValidator.Validate(driver.State.RoomId, sequence, driver.Commands(), driver.Events(), views, driver.State, driver.State.Tick, spectator));
        foreach (var tamper in new[] { "sourceObjectId", "cardNo", "effectKind", "sourceVisibility", "controllerId", "playAbility" }) {
            var row = new Dictionary<string, object?>(Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshots["P2"].Stack)));
            row[tamper] = tamper switch { "sourceObjectId" => "UNIT", "sourceVisibility" => "VISIBLE", "controllerId" => "P2", "playAbility" => false, _ => "FORGED" };
            var altered = views.ToDictionary(); altered["P2"] = views["P2"] with { Snapshot = snapshots["P2"] with { Stack = [row] } };
            var errors = MatchRecoveryValidator.Validate(driver.State.RoomId, sequence, driver.Commands(), driver.Events(), altered, driver.State, driver.State.Tick, spectator);
            Assert.NotEmpty(errors);
            var frame = new MatchRecoveryFrame(driver.State.RoomId, driver.State.Tick, sequence, driver.Commands(), driver.Events(), altered,
                errors, driver.State, initial, spectator) { OpeningSeed = initial.Seed };
            var rejected = Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), driver));
            Assert.Equal(ErrorCodes.RecoveryInconsistent, rejected.Code);
        }
        var leaked = new Dictionary<string, object?>(Assert.IsType<Dictionary<string, object?>>(Assert.Single(spectator.SpectatorSnapshot.Stack))) { ["sourceObjectId"] = "UNIT" };
        var forgedSpectator = spectator with { SpectatorSnapshot = spectator.SpectatorSnapshot with { Stack = [leaked] } };
        Assert.NotEmpty(MatchRecoveryValidator.Validate(driver.State.RoomId, sequence, driver.Commands(), driver.Events(), views,
            driver.State, driver.State.Tick, forgedSpectator));
        var changedEvents = driver.Events().Select(e => e.Event.Kind == "TRIGGER_QUEUED"
            ? e with { Event = e.Event with { Payload = new Dictionary<string, object?>(e.Event.Payload) { ["sourceObjectId"] = "OTHER" } } } : e).ToArray();
        var mismatch = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State, new CoreRuleEngine(), default, changedEvents);
        Assert.False(mismatch.IsMatch); await driver.Replay();
        // Resolution after departure is asserted separately by the original
        // VenUnitEntryBasicTests; this case isolates the persisted public view.
    }

    [Theory]
    [InlineData("VEN·026")]
    [InlineData("VEN·048")]
    public async Task TheSamePublicSkillAlsoMasksAnOpponentsCurrentMainDeckSource(string no)
    {
        var driver = new Driver(VenUnitEntryBasicTests.Position(no)); await Enter(driver, no);
        // An explicit projection fixture isolates the hidden main-deck boundary;
        // it is not submitted as a fabricated gameplay or recovery history.
        var state = driver.State with {
            PlayerZones = driver.State.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1"
                ? p.Value with { Base = p.Value.Base.Where(id => id != "UNIT").ToArray(), MainDeck = ["UNIT", ..p.Value.MainDeck] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(driver.State.ObjectLocations) { ["UNIT"] = new("P1", "MAIN_DECK") },
            CardObjects = new Dictionary<string, CardObjectState>(driver.State.CardObjects) {
                ["UNIT"] = driver.State.CardObjects["UNIT"] with { ObjectGeneration = driver.State.CardObjects["UNIT"].ObjectGeneration + 1 } } };
        AssertFace(ResolutionResult.BuildSnapshots(state)["P2"], no, "HIDDEN");
        AssertFace(ResolutionResult.BuildSpectatorSnapshot(state), no, "HIDDEN");
    }

    [Theory]
    [InlineData("VEN·026")]
    [InlineData("VEN·048")]
    public async Task LeavingAndBeingPlayedAgainKeepsBothIndependentSkillsAndTheirOriginalActionLog(string no)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(VenUnitEntryBasicTests.Position(no), "BLINK", "UNL-184/219");
        var driver = new Driver(initial); await Enter(driver, no);
        var oldGeneration = driver.State.CardObjects["UNIT"].ObjectGeneration;
        await driver.Act("P1", new PlayCardCommand("BLINK", "UNL-184/219", ["UNIT"])); await driver.Top();
        Assert.NotNull(driver.State.PendingEffectPlay);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        Assert.True(driver.State.CardObjects["UNIT"].ObjectGeneration > oldGeneration);
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Battlefields);
        if (driver.State.PendingCardChoice is { } choice)
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        Assert.Equal(2, driver.State.StackItems.Count);
        await driver.Top(); await driver.Top(); Assert.Empty(driver.State.StackItems);
        if (no == "VEN·026") Assert.Equal(initial.CardObjects["ALLY"].Power + 6, driver.State.CardObjects["ALLY"].Power);
        else Assert.Equal(new[] { "P1-DRAW-1", "P1-DRAW-2" }, driver.State.PlayerZones["P1"].Hand);
        Assert.Single(driver.Events(), e => e.Event.Kind == "UNIT_PLAYED_TO_BASE");
        Assert.Single(driver.Events(), e => e.Event.Kind == "UNIT_PLAYED_TO_BATTLEFIELD");
        await driver.Replay();
    }

    private static async Task Enter(Driver driver, string no)
    {
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        if (driver.State.PendingCardChoice is { } choice)
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
    }

    private static void AssertFace(SnapshotDto snapshot, string no, string source)
    {
        var row = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
        Assert.Equal(no, row["cardNo"]); Assert.Equal(source, row["sourceObjectId"]);
        Assert.Equal(source == "HIDDEN" ? "HIDDEN" : "VISIBLE", row["sourceVisibility"]);
        Assert.Equal(true, row["playAbility"]);
        Assert.Equal(no == "VEN·026" ? "WAR_BAND_PLAY_UNIT_POWER_PLUS_3" : no == "VEN·048" ? "CLOUD_DRAKE_PLAY_UNIT_DRAW_1" : "YORDLE_INSTRUCTOR_PLAY_UNIT_DRAW_1", row["effectKind"]);
    }

    private static IEnumerable<string> Strings(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.String) yield return node.GetString()!;
        if (node.ValueKind == JsonValueKind.Array) foreach (var child in node.EnumerateArray()) foreach (var value in Strings(child)) yield return value;
        if (node.ValueKind == JsonValueKind.Object) foreach (var child in node.EnumerateObject()) foreach (var value in Strings(child.Value)) yield return value;
    }
}
