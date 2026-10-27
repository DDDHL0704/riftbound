using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.StandbyDefendObserverTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class MandatoryRecastOptionalDiscountTests
{
    [Theory]
    [InlineData("decline")] [InlineData("pay")] [InlineData("discard")]
    public async Task ARealHoldTaxDoesNotMakeOptionalDiscardMandatoryWhileRecasting(string choice)
    {
        var initial = Position(); var game = new Driver(initial);
        ResolutionResult? core = null, native = null;
        PlayCostQuoteDto? defaultQuote = null, discountQuote = null;
        try
        {
            var begun = await game.Act("P1", new PassPriorityCommand());
            await TurnSequenceTestDriver.Complete(begun,
                (player, command) => new ValueTask<ResolutionResult>(game.Act(player, command)));
            Assert.Equal(MatchPhases.Main, game.State.Phase);
            Assert.Contains(game.State.UntilEndOfTurnEffects,
                e => e.StartsWith("BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:P1", StringComparison.Ordinal));
            Assert.All(game.State.RunePools.Values, p => Assert.Equal(RunePool.Empty, p));
            var purple = game.State.PlayerZones["P1"].Base.Where(id => game.State.CardObjects[id].CardNo == "UNL-R05")
                .Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(10, purple.Length);
            foreach (var id in purple.Take(7)) await game.Act("P1", new TapRuneCommand(id));
            foreach (var id in purple.Skip(7).Take(2)) await game.Act("P1", new RecycleRuneCommand(id));
            await game.Act("P1", new PlayCardCommand("PARENT", "OGN·198/298", ["UNIT"]));
            await game.Top();
            var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
            Assert.False(pending.Optional); Assert.True(pending.IgnoreBaseMana); Assert.False(pending.IgnoreAllCosts);
            Assert.Equal(1, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            var play = new PlayCardCommand("UNIT", "OGN·002/298", []);
            var discounted = play with { OptionalCosts = ["DISCARD_HAND_CARD:FODDER"] };
            var engine = new CoreRuleEngine();
            defaultQuote = engine.PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play));
            discountQuote = engine.PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, discounted));
            Assert.True(defaultQuote.CanPay, defaultQuote.Message); Assert.True(discountQuote.CanPay, discountQuote.Message);
            Assert.Equal(1, defaultQuote.Cost!.Mana); Assert.Equal(0, discountQuote.Cost!.Mana);
            Assert.Equal(0, defaultQuote.Cost.GenericPower + defaultQuote.Cost.PowerByTrait.Values.Sum());
            var hand = game.State.PlayerZones["P1"].Hand.ToArray();
            if (choice == "decline")
            {
                var refusal = new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []);
                core = await engine.ResolveAsync(game.State, new("optional-discard-refusal-core", "P1", CommandTypes.ChooseCards), refusal, default);
                var session = new MatchSession(game.State, engine, NoopMatchJournal.Instance);
                var prompt = session.PromptFor("P1");
                var node = JsonSerializer.SerializeToNode(refusal, Json)!.AsObject();
                node["promptId"] = prompt.PromptId; node["snapshotTick"] = game.State.Tick;
                var raw = JsonSerializer.SerializeToElement(node, Json);
                native = await session.SubmitAsync("P1", "optional-discard-refusal-native", GameCommandJsonMapper.Map(raw), raw, default);
                Assert.True(core.Accepted, core.ErrorMessage); Assert.True(native.Accepted, native.ErrorMessage);
                Assert.Contains(prompt.Candidates!, c => c.Action == CommandTypes.ChooseCards && c.Enabled);
                await game.Act("P1", refusal);
                Assert.Equal(hand, game.State.PlayerZones["P1"].Hand);
                Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
                Assert.DoesNotContain(game.State.CardPlays.Finalized, p => p.Source.ObjectId == "UNIT");
                Assert.Equal(1, game.State.RunePools["P1"].Mana);
            }
            else
            {
                await game.Act("P1", choice == "discard" ? discounted : play);
                for (var i = 0; game.State.StackItems.Count > 0 && i < 6; i++) await game.Top();
                Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
                Assert.Equal(choice == "discard" ? 1 : 0, game.State.RunePools["P1"].Mana);
                if (choice == "discard")
                {
                    Assert.DoesNotContain("FODDER", game.State.PlayerZones["P1"].Hand);
                    Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard);
                }
                else Assert.Equal(hand, game.State.PlayerZones["P1"].Hand);
            }
            Assert.Null(game.State.PendingEffectPlay); Assert.Contains("PARENT", game.State.PlayerZones["P1"].Graveyard);
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
                engine, default, game.Events());
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_MANDATORY_RECAST_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "optional-discount-" + choice + ".json"),
                    JsonSerializer.Serialize(new { scope = "Explicit turn-five beginning, zero pools and twelve physical runes; actual held ability, channel/draw, rune payments and Harrowing. Default one-mana recast versus voluntary discard-to-zero; original accepted driver history checks privacy, recovery, dedup and replay. Separate current-state Core/native refusal probes do not replace birth or persistence proof. No complete room opening/L2/L3/SQL/network/restart/whole-card claim.",
                        initial, state = game.State, core, native, defaultQuote, discountQuote,
                        commands = game.Commands(), events = game.Events() }, Json));
            }
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static MatchState Position()
    {
        var cards = new Dictionary<string, CardObjectState>(); var at = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string zone, string owner = "P1", string? field = null)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner); at[id] = new(owner, zone, field); }
        Put("PARENT", "OGN·198/298", "HAND"); Put("UNIT", "OGN·002/298", "GRAVEYARD");
        Put("FODDER", "OGN·096/298", "HAND"); Put("FIELD", "UNL-219/219", "BATTLEFIELD", field: "FIELD");
        cards["FIELD"] = cards["FIELD"] with { ControllerId = "P1" };
        Put("HOST", "OGN·096/298", "BATTLEFIELD", field: "FIELD");
        Put("OTHER", "OGN·294/298", "BATTLEFIELD", "P2", "OTHER");
        var deck = Enumerable.Range(0, 3).Select(i => "DRAW" + i).ToArray();
        foreach (var id in deck) Put(id, "OGN·096/298", "MAIN_DECK");
        foreach (var i in Enumerable.Range(1, 8)) Put("R" + i, "UNL-R05", "BASE");
        Put("BLUE", "UNL-R03", "BASE");
        var runeDeck = new[] { "CHANNEL1", "CHANNEL2", "CHANNEL3" };
        foreach (var id in runeDeck) Put(id, "UNL-R05", "RUNE_DECK");
        return new("MANDATORY-RECAST-OPTIONAL-DISCOUNT", 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, status: MatchStatuses.InProgress,
            phase: MatchPhases.TurnStart, timingState: TimingStates.NeutralClosed,
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = ["PARENT", "FODDER"], Graveyard = ["UNIT"],
                    Base = Enumerable.Range(1, 8).Select(i => "R" + i).Append("BLUE").ToArray(),
                    MainDeck = deck, RuneDeck = runeDeck, Battlefields = ["FIELD", "HOST"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["OTHER"] } }, cardObjects: cards, objectLocations: at, seed: 424242);
    }
}
