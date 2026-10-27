using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.StandbyDefendObserverTests.Driver;

namespace Riftbound.ConformanceTests;

// CN445.2/445.2.a permits refusing a nonzero resource fee even when the
// enclosing play instruction is mandatory. CN445.1.b keeps zero nonrefusable.
public sealed class MandatoryRecastResourcePaymentTests
{
    private const string Parent = "OGN·198/298";
    private const string PaidUnit = "OGN·118/298";
    private const string FreeUnit = "OGN·096/298";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AForcedRecastCanRefuseItsNonzeroResourceFeeEvenWhenAlreadyFunded(bool fundedBefore)
        => await Run(PaidUnit, fundedBefore, decline: true);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PayingTheForcedRecastStillDebitsItsActualPrintedBlueFee(bool fundedBefore)
        => await Run(PaidUnit, fundedBefore, decline: false);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AFreeForcedRecastCannotRefuseAZeroResourceFee(bool fundedBefore)
        => await Run(FreeUnit, fundedBefore, decline: true);

    private static async Task Run(string unit, bool fundedBefore, bool decline)
    {
        var initial = Position(unit); var game = new Driver(initial);
        ResolutionResult? core = null, native = null;
        try
        {
            foreach (var rune in Enumerable.Range(1, 6)) await game.Act("P1", new TapRuneCommand("R" + rune));
            await game.Act("P1", new RecycleRuneCommand("R7"));
            await game.Act("P1", new RecycleRuneCommand("R8"));
            if (fundedBefore) await game.Act("P1", new RecycleRuneCommand("BLUE"));
            await game.Act("P1", new PlayCardCommand("PARENT", Parent, ["UNIT"]));
            await game.Top();
            var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
            Assert.False(pending.Optional); Assert.True(pending.IgnoreBaseMana); Assert.False(pending.IgnoreBasePower);
            Assert.Equal(0, game.State.RunePools["P1"].Mana);
            Assert.Equal(fundedBefore ? 1 : 0, game.State.RunePools["P1"].TotalPower);
            var before = MatchStateHasher.Hash(game.State);
            var priorCommands = JsonSerializer.Serialize(game.Commands(), Json);
            var priorEvents = JsonSerializer.Serialize(game.Events(), Json);
            var refusal = new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []);
            if (decline)
            {
                core = await new CoreRuleEngine().ResolveAsync(game.State,
                    new("mandatory-resource-refusal-core", "P1", CommandTypes.ChooseCards), refusal, default);
                // Probe the native prompt/tick path separately before asserting,
                // retaining both actual results even when this is a red fixture.
                var session = new MatchSession(game.State, new CoreRuleEngine());
                var prompt = session.PromptFor("P1");
                var raw = JsonSerializer.SerializeToNode(refusal, Json)!.AsObject();
                raw["promptId"] = prompt.PromptId; raw["snapshotTick"] = game.State.Tick;
                var wire = JsonSerializer.SerializeToElement(raw, Json);
                native = await session.SubmitAsync("P1", "mandatory-resource-refusal-native",
                    GameCommandJsonMapper.Map(wire), wire, default);
                var accepted = unit == PaidUnit;
                Assert.Equal(accepted, core.Accepted); Assert.Equal(accepted, native.Accepted);
                Assert.Equal(before, MatchStateHasher.Hash(game.State));
                if (accepted)
                {
                    await game.Act("P1", refusal);
                    Assert.Null(game.State.PendingEffectPlay);
                    Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
                    Assert.DoesNotContain(game.State.CardPlays.Finalized, p => p.Source.ObjectId == "UNIT");
                    Assert.Equal(fundedBefore ? 1 : 0, game.State.RunePools["P1"].TotalPower);
                    Assert.Contains("PARENT", game.State.PlayerZones["P1"].Graveyard);
                    Assert.Equal(priorCommands, JsonSerializer.Serialize(game.Commands().Take(game.Commands().Length - 1), Json));
                    Assert.Equal(priorEvents, JsonSerializer.Serialize(game.Events().Take(JsonSerializer.Deserialize<JsonElement[]>(priorEvents)!.Length), Json));
                    await Replay(); return;
                }
                Assert.Equal(before, MatchStateHasher.Hash(core.State)); Assert.Empty(core.Events);
                Assert.Equal(before, MatchStateHasher.Hash(native.State)); Assert.Empty(native.Events);
            }
            if (!fundedBefore && unit == PaidUnit) await game.Act("P1", new RecycleRuneCommand("BLUE"));
            var play = new PlayCardCommand("UNIT", unit, []);
            var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play));
            Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
            Assert.Equal(unit == PaidUnit ? 1 : 0, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Blue));
            await game.Act("P1", play);
            for (var i = 0; game.State.StackItems.Count > 0 && i < 6; i++) await game.Top();
            Assert.Null(game.State.PendingEffectPlay); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
            Assert.Equal(unit == PaidUnit ? 0 : fundedBefore ? 1 : 0, game.State.RunePools["P1"].TotalPower);
            Assert.Contains("PARENT", game.State.PlayerZones["P1"].Graveyard);
            await Replay();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_MANDATORY_RECAST_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, $"{unit.Replace('/', '_')}-{fundedBefore}-{decline}.json"),
                    JsonSerializer.Serialize(new { scope = "Explicit turn-five normal-card midgame, zero initial pools, nine physical runes. Actual tap/recycle/parent play/pass and fee refusal or paid/free recast. Both independent Core and bound prompt/tick native refusal results retained; accepted original driver history checks privacy, recovery, dedup and replay. No complete opening, SQL, network, restart, L2/L3 or whole-card claim.",
                        initial, state = game.State, core, native, commands = game.Commands(), events = game.Events() }, Json));
            }
        }
        async Task Replay()
        {
            var result = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
                new CoreRuleEngine(), default, game.Events());
            Assert.True(result.IsMatch, string.Join("; ", result.Errors));
        }
    }

    private static MatchState Position(string unit)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string zone)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1"); locations[id] = new("P1", zone); }
        Put("PARENT", Parent, "HAND"); Put("UNIT", unit, "GRAVEYARD");
        foreach (var i in Enumerable.Range(1, 8)) Put("R" + i, "UNL-R05", "BASE");
        Put("BLUE", "UNL-R03", "BASE");
        return new("MANDATORY-RECAST-RESOURCE", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = ["PARENT"], Graveyard = ["UNIT"], Base = Enumerable.Range(1, 8).Select(i => "R" + i).Append("BLUE").ToArray() },
                ["P2"] = PlayerZones.Empty }, cardObjects: cards, objectLocations: locations, seed: 424242);
    }
}
