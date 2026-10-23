using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Sdk;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OptionalEntryNegativeHistoryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("OGN·010/298", 2)]
    [InlineData("OGN·271/298", 1)]
    public async Task ThePrintedUnflooredReductionCanLeaveAnUndamagedUnitAtZeroOrBelowUntilExpiry(string targetNo, int originalPower)
    {
        var initial = Start(); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", targetNo, "P1") } };
        var driver = await Apply(initial, true); var target = driver.State.CardObjects["ALLY"];
        Assert.Equal(originalPower - 2, target.Power); Assert.Equal(0, target.Damage);
        Assert.Contains("ALLY", driver.State.PlayerZones["P1"].Base);
        var modifier = Assert.Single(target.UntilEndOfTurnPowerModifiers);
        Assert.Equal(-2, modifier.PowerDelta); Assert.Equal(-2, modifier.RequestedPowerDelta);
        Assert.Equal(0, modifier.MinimumPower); Assert.Equal(originalPower, modifier.BasePower);
        Assert.Equal(originalPower - 2, modifier.ResultingPower); Assert.Null(modifier.ChosenNegativeMight);
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal(originalPower, driver.State.CardObjects["ALLY"].Power);
        Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        await driver.Replay(); Export(targetNo.Replace('·', '-').Replace('/', '-'), initial, driver);
    }

    [Fact]
    public async Task NegativeHistoryKeepsItsOwnBaseAcrossLaterBoonPositiveModifierAndExpiry()
    {
        var initial = OptionalEntryTargetConfirmationTests.AddSpell(Start(), "BOON", "OGN·053/298");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BAND"] = OfficialGarenConquestBattlefieldCountTests.Printed("BAND", "VEN·026", "P1") },
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [..p.Value.Hand, "BAND"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["BAND"] = new("P1", "HAND") } };
        var driver = await Apply(initial, true);
        var first = Assert.Single(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(4, first.BasePower); Assert.Equal(2, first.ResultingPower);
        await driver.Act("P1", new PlayCardCommand("BOON", "OGN·053/298", ["ALLY"])); await driver.Top();
        Assert.Equal(3, driver.State.CardObjects["ALLY"].Power);
        Assert.Equal(first, Assert.Single(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers));
        await driver.Act("P1", new PlayCardCommand("BAND", "VEN·026", [], Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"])); await driver.Top();
        var modifiers = driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers;
        Assert.Equal(first, modifiers[0]); Assert.Equal(2, modifiers.Count);
        Assert.Equal(5, modifiers[1].BasePower); Assert.Equal(6, modifiers[1].ResultingPower); Assert.Equal(2, modifiers[1].AppliedOrder);
        Assert.Equal(6, driver.State.CardObjects["ALLY"].Power);
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal(5, driver.State.CardObjects["ALLY"].Power);
        Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        await driver.Replay(); Export(nameof(NegativeHistoryKeepsItsOwnBaseAcrossLaterBoonPositiveModifierAndExpiry), initial, driver);
    }

    [Fact]
    public async Task NegativeProvenanceRejectsWrongSignedAmountsFloorsContextsIdentitiesAndAdditionalPrivateReferences()
    {
        var initial = Start(); var driver = await Apply(initial, true); var original = driver.State;
        var hash = MatchStateHasher.Hash(original);
        var actual = Snapshot(original); OrdinaryPlayPrivacyAssertions.CheckHistoricalModifiers(actual, original);
        Assert.Equal("CHECKED_PUBLIC_PLAY_MODIFIER", Row(actual)["sourceObjectId"]!.GetValue<string>());
        foreach (var branch in new[] { "missing-play", "missing-finalized", "wrong-play", "same-generation", "wrong-generation",
            "source", "face", "target", "effect-id", "order", "amount", "sign", "effective", "resulting", "base", "source-path",
            "duplicate-view", "duplicate-ledger", "requested", "minimum", "snapshot-might", "chosen-negative" }) {
            var state = original; var snapshot = Snapshot(state); var row = Row(snapshot);
            var play = Assert.Single(state.CardPlays.Resolved, p => p.CardNo == "SFD·067/221");
            if (branch == "missing-play") state = state with { CardPlays = state.CardPlays with { Resolved = state.CardPlays.Resolved.Where(p => p != play).ToArray() } };
            if (branch == "missing-finalized") state = state with { CardPlays = state.CardPlays with { Finalized = state.CardPlays.Finalized.Where(p => p.PlayId != play.PlayId).ToArray() } };
            if (branch == "wrong-play") state = state with { CardPlays = state.CardPlays with { Resolved = state.CardPlays.Resolved.Select(p => p == play ? p with { PlayId = "FORGED" } : p).ToArray() } };
            if (branch is "same-generation" or "wrong-generation") {
                var generation = branch == "same-generation" ? state.CardObjects["UNIT"].ObjectGeneration : play.Source.Generation - 1;
                state = state with { CardPlays = state.CardPlays with {
                    Resolved = state.CardPlays.Resolved.Select(p => p == play ? p with { Source = p.Source with { Generation = generation } } : p).ToArray(),
                    Finalized = state.CardPlays.Finalized.Select(p => p.PlayId == play.PlayId && branch == "same-generation"
                        ? p with { Source = p.Source with { Generation = generation } } : p).ToArray() } };
            }
            if (branch == "source") row["sourceObjectId"] = "P1-DRAW-1";
            if (branch == "face") row["sourceCardNo"] = "VEN·048";
            if (branch == "target") row["targetObjectId"] = "ENEMY";
            if (branch == "effect-id") row["effectId"] = "POWER:FORGED";
            if (branch == "order") row["appliedOrder"] = 2;
            if (branch == "amount") row["powerDelta"] = -3;
            if (branch == "sign") row["powerDelta"] = 2;
            if (branch == "requested") row["requestedPowerDelta"] = 2;
            if (branch == "minimum") row["minimumPower"] = 1;
            if (branch == "snapshot-might") row["snapshotMight"] = 4;
            if (branch == "effective") row["effectivePower"] = 3;
            if (branch == "resulting") row["resultingPower"] = 3;
            if (branch == "base") row["basePower"] = 5;
            if (branch == "source-path") row["sourcePath"] = "FORGED";
            if (branch == "duplicate-view") snapshot["timing"]!["continuousEffects"]!.AsArray().Add(row.DeepClone());
            if (branch is "duplicate-ledger" or "chosen-negative") state = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key, p => p.Key == "ALLY"
                ? p.Value with { UntilEndOfTurnPowerModifiers = branch == "duplicate-ledger"
                    ? [..p.Value.UntilEndOfTurnPowerModifiers, p.Value.UntilEndOfTurnPowerModifiers[0]]
                    : [p.Value.UntilEndOfTurnPowerModifiers[0] with { ChosenNegativeMight = new(-2, state.Tick, "P1", play.PlayId, new("ALLY", p.Value.ObjectGeneration), []) }] } : p.Value) };
            var before = snapshot.ToJsonString();
            if (branch is not ("face" or "source")) {
                Assert.True(Record.Exception(() => OrdinaryPlayPrivacyAssertions.CheckHistoricalModifiers(snapshot, state)) is XunitException, branch);
                Assert.Equal(before, snapshot.ToJsonString());
            }
            Assert.True(Record.Exception(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(Result(state, snapshot))) is XunitException, branch);
        }
        var unrelated = Snapshot(original); unrelated["timing"]!["additionalPrivateReference"] = "UNIT";
        Assert.True(Record.Exception(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(Result(original, unrelated))) is XunitException);
        Assert.Equal(hash, MatchStateHasher.Hash(original)); await driver.Replay(); Export("negative-guard", initial, driver);
    }

    [Fact]
    public async Task CoordinatedNegativeHistoryWithoutPayingTheOriginalOptionalCostFailsIndependentReplay()
    {
        var initial = Start(); var paid = await Apply(initial, true); var unpaid = await Apply(initial, false);
        Assert.Empty(unpaid.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(6, unpaid.State.RunePools["P1"].TotalPower);
        var forged = unpaid.State with { CardPlays = paid.State.CardPlays,
            CardObjects = new Dictionary<string, CardObjectState>(unpaid.State.CardObjects) { ["ALLY"] = paid.State.CardObjects["ALLY"] } };
        // Even coordinated historical provenance is insufficient evidence of
        // payment. The original commands, events and final state must agree.
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(Result(forged, Snapshot(forged)));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, unpaid.Commands(), forged,
            new CoreRuleEngine(), default, unpaid.Events());
        Assert.False(replay.IsMatch);
        await paid.Replay(); await unpaid.Replay();
        Export("paid-original", initial, paid); Export("unpaid-original", initial, unpaid);
    }

    private static MatchState Start() => OptionalEntryTargetConfirmationTests.AddSpell(OptionalEntryTargetConfirmationTests.Position("SFD·067/221"), "RETURN", "OGN·104/298");

    private static async Task<Driver> Apply(MatchState initial, bool paid)
    {
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", "SFD·067/221", [], OptionalCosts: paid ? OptionalEntryTargetConfirmationTests.Costs("SFD·067/221") : [], Destination: "BASE"));
        if (driver.State.PendingCardChoice is { } choice)
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top();
        if (paid) await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand); await driver.Replay(); return driver;
    }

    private static JsonNode Snapshot(MatchState state) => JsonSerializer.SerializeToNode(ResolutionResult.BuildSnapshots(state)["P2"], Json)!;
    private static JsonObject Row(JsonNode snapshot) => Assert.Single(snapshot["timing"]!["continuousEffects"]!.AsArray().OfType<JsonObject>(),
        row => row["sourceCardNo"]?.GetValue<string>() == "SFD·067/221");
    private static ResolutionResult Result(MatchState state, JsonNode snapshot)
    {
        var views = ResolutionResult.BuildSnapshots(state).ToDictionary(); views["P2"] = snapshot.Deserialize<SnapshotDto>(Json)!;
        return new(true, null, state, [], views, ResolutionResult.BuildPrompts(state));
    }

    private static void Export(string name, MatchState initial, Driver driver)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_OPTIONAL_ENTRY_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(new {
            initial, final = driver.State, commands = driver.Commands(), events = driver.Events()
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
