using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Sdk;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OrdinaryPlayPrivacyGuardTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task OnlyTheExactOriginalPublicModifierCanBeExemptedFromThePrivateIdentityScan()
    {
        var (_, driver) = await Resolved(); var original = driver.State; var hash = MatchStateHasher.Hash(original);
        var actual = Snapshot(original); OrdinaryPlayPrivacyAssertions.CheckHistoricalModifiers(actual, original);
        Assert.Equal("CHECKED_PUBLIC_PLAY_MODIFIER", Row(actual)["sourceObjectId"]!.GetValue<string>());
        foreach (var branch in new[] { "missing-play", "missing-finalized", "wrong-play", "same-generation", "wrong-generation",
            "source", "face", "target", "effect-id", "order", "amount", "effective", "resulting", "base", "source-path",
            "duplicate-view", "duplicate-ledger", "requested", "minimum", "snapshot-might" })
        {
            var state = original; var snapshot = Snapshot(state); var row = Row(snapshot);
            var play = Assert.Single(state.CardPlays.Resolved, p => p.CardNo == "VEN·026");
            if (branch == "missing-play") state = state with { CardPlays = state.CardPlays with { Resolved = state.CardPlays.Resolved.Where(p => p != play).ToArray() } };
            if (branch == "missing-finalized") state = state with { CardPlays = state.CardPlays with { Finalized = state.CardPlays.Finalized.Where(p => p.PlayId != play.PlayId).ToArray() } };
            if (branch == "wrong-play") state = state with { CardPlays = state.CardPlays with { Resolved = state.CardPlays.Resolved.Select(p => p == play ? p with { PlayId = "STACK-999-UNIT" } : p).ToArray() } };
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
            if (branch == "amount") row["powerDelta"] = 4;
            if (branch == "requested") row["requestedPowerDelta"] = 4;
            if (branch == "minimum") row["minimumPower"] = 1;
            if (branch == "snapshot-might") row["snapshotMight"] = 4;
            if (branch == "effective") row["effectivePower"] = 8;
            if (branch == "resulting") row["resultingPower"] = 8;
            if (branch == "base") row["basePower"] = 5;
            if (branch == "source-path") row["sourcePath"] = "FORGED";
            if (branch == "duplicate-view") snapshot["timing"]!["continuousEffects"]!.AsArray().Add(row.DeepClone());
            if (branch == "duplicate-ledger") state = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key,
                p => p.Key == "ALLY" ? p.Value with { UntilEndOfTurnPowerModifiers = [..p.Value.UntilEndOfTurnPowerModifiers, p.Value.UntilEndOfTurnPowerModifiers[0]] } : p.Value) };
            var before = snapshot.ToJsonString();
            if (branch is not ("face" or "source")) {
                var error = Record.Exception(() => OrdinaryPlayPrivacyAssertions.CheckHistoricalModifiers(snapshot, state));
                Assert.True(error is XunitException, branch + " must fail the independent provenance guard");
                Assert.Equal(before, snapshot.ToJsonString());
            }
            var result = Result(state, snapshot);
            Assert.True(Record.Exception(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(result)) is XunitException,
                branch + " must fail either exact provenance or the original hidden-zone scan");
        }
        var unrelated = Snapshot(original); unrelated["timing"]!["unrelatedPrivateIdentity"] = "UNIT";
        Assert.True(Record.Exception(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(Result(original, unrelated))) is XunitException);
        Assert.Equal(hash, MatchStateHasher.Hash(original)); await driver.Replay();
    }

    [Fact]
    public async Task CoordinatedHistoricalAndArithmeticChangesStillFailTheCompleteOriginalLogReplay()
    {
        var (initial, driver) = await Resolved(); var state = driver.State;
        var play = Assert.Single(state.CardPlays.Resolved, p => p.CardNo == "VEN·026");
        var oldGeneration = state with { CardPlays = state.CardPlays with {
            Resolved = state.CardPlays.Resolved.Select(p => p == play ? p with { Source = p.Source with { Generation = 0 } } : p).ToArray(),
            Finalized = state.CardPlays.Finalized.Select(p => p.PlayId == play.PlayId ? p with { Source = p.Source with { Generation = 0 } } : p).ToArray() } };
        var modifier = Assert.Single(state.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        var changedAmount = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key, p => p.Key == "ALLY"
            ? p.Value with { Power = 8, UntilEndOfTurnPowerModifier = 4, UntilEndOfTurnPowerModifiers = [modifier with {
                PowerDelta = 4, RequestedPowerDelta = 4, EffectivePower = 8, ResultingPower = 8,
                EffectId = modifier.EffectId!.Replace(":3:1", ":4:1", StringComparison.Ordinal) }] } : p.Value) };
        foreach (var forged in new[] { oldGeneration, changedAmount }) {
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged,
                new CoreRuleEngine(), default, driver.Events());
            Assert.False(replay.IsMatch);
        }
        await driver.Replay();
    }

    private static async Task<(MatchState, Driver)> Resolved()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(VenUnitEntryBasicTests.Position("VEN·026"), "RETURN", "OGN·104/298");
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·026", [], Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top(); await driver.Top();
        Assert.Equal(7, driver.State.CardObjects["ALLY"].Power); return (initial, driver);
    }

    private static JsonNode Snapshot(MatchState state) => JsonSerializer.SerializeToNode(ResolutionResult.BuildSnapshots(state)["P2"], Json)!;
    private static JsonObject Row(JsonNode snapshot) => Assert.Single(snapshot["timing"]!["continuousEffects"]!.AsArray().OfType<JsonObject>(),
        row => row["sourceCardNo"]?.GetValue<string>() == "VEN·026");
    private static ResolutionResult Result(MatchState state, JsonNode snapshot)
    {
        var views = ResolutionResult.BuildSnapshots(state).ToDictionary(); views["P2"] = snapshot.Deserialize<SnapshotDto>(Json)!;
        return new(true, null, state, [], views, ResolutionResult.BuildPrompts(state));
    }
}
