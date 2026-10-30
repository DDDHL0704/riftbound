using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.StandbyDefendObserverTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class MandatoryRecastReducedPowerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CompulsoryEmpoweredPowerReductionDeterminesWhetherTheActualFeeCanBeRefused(bool empower)
    {
        // Explicit midgame: the normal reducer is not empowered initially. All
        // resource acquisition, Empower, parent play and choices are real commands.
        var initial = DeckPlaySequenceTests.TopCard(DeckPlaySequenceTests.Position("OGN·115/298"), "P1_TOP", "OGN·093/298");
        var cards = initial.CardObjects.ToDictionary(x => x.Key, x => x.Value);
        var locations = initial.ObjectLocations.ToDictionary(x => x.Key, x => x.Value);
        cards["REDUCER"] = OfficialGarenConquestBattlefieldCountTests.Printed("REDUCER", "VEN·055", "P1");
        locations["REDUCER"] = new("P1", "BASE");
        var runes = Enumerable.Range(1, 12).Select(i => "REDUCED-FEE-RUNE-" + i).ToArray();
        foreach (var id in runes) { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R03", "P1"); locations[id] = new("P1", "BASE"); }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "REDUCER", ..runes] } } };
        var game = new Driver(initial); var engine = new CoreRuleEngine();
        ResolutionResult? core = null, native = null; PlayCostQuoteDto? quote = null;
        try
        {
            await game.Act("P1", new TapRuneCommand(runes[0], runes));
            await game.Act("P1", new RecycleRuneCommand(runes[0])); await game.Act("P1", new RecycleRuneCommand(runes[1]));
            if (empower) { await game.Act("P1", new ActivateAbilityCommand("REDUCER", "EMPOWER:0:0", [])); await game.Top(); }
            Assert.Equal(empower, game.State.CardObjects["REDUCER"].IsEmpowered);
            await game.Act("P1", new PlayCardCommand("PARENT", "OGN·115/298", [])); await game.Top();
            while (game.State.PendingCardChoice is { } choice)
                await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                    [choice.PlayerId == "P1" ? "P1_TOP" : "P2_TOP"]));
            Assert.Equal("P2", game.State.PendingEffectPlay!.PlayerId);
            await game.Act("P2", new PlayCardCommand("P2_TOP", "OGN·096/298", []));
            var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
            Assert.Equal("P1", pending.PlayerId); Assert.False(pending.Optional);
            var play = new PlayCardCommand("P1_TOP", "OGN·093/298", ["P2_TOP"]);
            quote = engine.PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play));
            Assert.True(quote.CanPay, quote.Message); Assert.Equal(0, quote.Cost!.Mana);
            Assert.Equal(1, quote.Cost.PrintedPower); Assert.Equal(empower ? 0 : 1, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Blue));
            Assert.Equal(0, quote.Cost.GenericPower);
            var before = MatchStateHasher.Hash(game.State); var pool = game.State.RunePools["P1"];
            var decline = new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []);
            core = await engine.ResolveAsync(game.State, new("reduced-fee-core", "P1", decline.CmdType), decline, default);
            var session = new MatchSession(game.State, engine, NoopMatchJournal.Instance);
            var originalPrompt = session.PromptFor("P1");
            var raw = JsonSerializer.SerializeToNode(decline, Json)!.AsObject();
            raw["promptId"] = originalPrompt.PromptId; raw["snapshotTick"] = game.State.Tick;
            var wire = JsonSerializer.SerializeToElement(raw, Json);
            native = await session.SubmitAsync("P1", "reduced-fee-native", GameCommandJsonMapper.Map(wire), wire, default);
            Assert.Equal(!empower, core.Accepted); Assert.Equal(!empower, native.Accepted);
            Assert.Equal(before, MatchStateHasher.Hash(game.State));
            Assert.Equal(!empower, originalPrompt.Candidates!.Any(c => c.Action == CommandTypes.ChooseCards && c.Enabled));
            if (empower)
            {
                Assert.Equal(ErrorCodes.InvalidTarget, core.ErrorCode); Assert.Equal(ErrorCodes.InvalidTarget, native.ErrorCode);
                Assert.Equal(before, MatchStateHasher.Hash(core.State)); Assert.Empty(core.Events);
                Assert.Equal(before, MatchStateHasher.Hash(native.State)); Assert.Empty(native.Events);
                await game.Act("P1", play);
                while (game.State.StackItems.Count > 0) await game.Top();
                Assert.Contains("P1_TOP", game.State.PlayerZones["P1"].Graveyard);
                Assert.Contains(game.State.CardPlays.Finalized, p => p.Source.ObjectId == "P1_TOP");
            }
            else
            {
                await game.Act("P1", decline);
                Assert.Contains("P1_TOP", game.State.PlayerZones["P1"].Banished);
                Assert.DoesNotContain(game.State.CardPlays.Finalized, p => p.Source.ObjectId == "P1_TOP");
            }
            Assert.Equal(pool, game.State.RunePools["P1"]); Assert.Null(game.State.PendingEffectPlay);
            await RideTheWindCurrentMovementTests.Replay(initial, game);
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_REDUCED_RECAST_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "empowered-" + empower + ".json"), JsonSerializer.Serialize(new {
                    scope = "Declared normal-card midgame component with zero initial pools and twelve physical runes. Actual resource/Empower/BrightFuture/selection/opponent play commands derive mandatory spell. Current quote and Core/native empty refusal results retained independently; accepted driver history checks privacy/dedup/recovery/replay. No canonical birth, SQL/network/restart/full L2/L3 or whole-card claim.",
                    initial, empower, quote, core, native, final = game.State, commands = game.Commands(), events = game.Events() }, Json));
            }
        }
    }
}
