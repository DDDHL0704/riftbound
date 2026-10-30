using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven124LastBreathCostInteractionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("OGN·096/298", 0, 2, 0, 1, 0, 0, 0)]
    [InlineData("OGN·096/298", 2, 2, 0, 1, 0, 0, 0)]
    [InlineData("OGN·075/298", 0, 7, 0, 1, 2, 0, 0)]
    [InlineData("OGN·075/298", 2, 7, 0, 1, 2, 0, 0)]
    [InlineData("OGN·178/298", 0, 5, 1, 2, 0, 2, 3)]
    [InlineData("OGN·178/298", 2, 5, 1, 2, 0, 2, 3)]
    [InlineData("OGN·178/298", 2, 5, 1, 2, 0, 2, 0)]
    public async Task ActualRequiredDestructionEarnsEveryPaidTextBeforeTheParentEmpowerCanResolve(
        string victimNo, int copies, int unitMana, int unitPower, int drawCount, int runeCount, int discardCount, int held)
    {
        var initial = Position(victimNo, copies, unitMana, unitPower, held); var game = new Driver(initial);
        try
        {
            var neededMana = 3 + unitMana + copies;
            await game.TapRunes("P1", Enumerable.Range(0, neededMana).Select(i => "COST-RUNE-" + i));
            Assert.Equal(neededMana, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            if (unitPower == 1)
            {
                await game.Act("P1", new RecycleRuneCommand("COST-RUNE-0"));
                Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Purple]);
            }
            await game.Act("P1", new PlayCardCommand("VICTIM", victimNo, []));
            Assert.Equal(3 + copies, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            var victimGeneration = game.State.CardObjects["VICTIM"].ObjectGeneration;
            for (var i = 0; i < copies; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("COST-RUNE-" + (i + unitPower)));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "VICTIM", ["ASSEMBLE_GREEN"])); await game.Top();
                Assert.Equal(victimNo, game.State.CardObjects["SONG-" + i].AttachedRuleText!.CardNo);
                Assert.Equal(victimGeneration, game.State.CardObjects["SONG-" + i].AttachedRuleText!.Unit.Generation);
                Assert.Equal(3 + copies - i - 1, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            }
            await game.Act("P1", new PlayCardCommand("UNIT", "VEN·124", []));
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            var candidate = Assert.Single(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
            var row = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]),
                r => (string?)r["sourceObjectId"] == "UNIT" && (string?)r["abilityId"] == "EMPOWER:0:0");
            Assert.Equal(1, row["requiredAdditionalCostChoiceCount"]);
            var choice = Assert.Single(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(row["additionalCostChoices"]),
                c => c.ObjectIds is not null && c.ObjectIds.SequenceEqual(["VICTIM"]));
            var prefix = game.Events().Length; var deckAtPayment = game.State.PlayerZones["P1"].MainDeck.ToArray();
            var runesAtPayment = game.State.PlayerZones["P1"].RuneDeck.ToArray(); var handAtPayment = game.State.PlayerZones["P1"].Hand.ToArray();
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [], OptionalCosts: [choice.Id]));
            Assert.Contains("VICTIM", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(victimGeneration + 1, game.State.CardObjects["VICTIM"].ObjectGeneration);
            var parent = Assert.Single(game.State.StackItems, s => s.EmpowerActivation is not null);
            Assert.Empty(parent.TargetObjectIds); Assert.Equal(0, parent.EmpowerActivation!.PaidMana);
            Assert.Equal(new ObjectBinding("VICTIM", victimGeneration), parent.EmpowerActivation.DestructionCost!.Unit);
            Assert.Equal(handAtPayment, game.State.PlayerZones["P1"].Hand); Assert.Equal(deckAtPayment, game.State.PlayerZones["P1"].MainDeck);
            Assert.Equal(runesAtPayment, game.State.PlayerZones["P1"].RuneDeck);
            Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind is "CARD_DRAWN" or "CARD_DISCARDED" or "RUNES_CALLED");
            var queue = game.State.TriggerQueue.Where(t => t.SourceObjectId == "VICTIM").ToArray();
            Assert.Equal(1 + copies, queue.Length + game.State.StackItems.Count(s => s.SourceObjectId == "VICTIM"));
            if (queue.Length > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: queue.Select(t => t.TriggerId).ToArray()));
            Assert.Equal(copies + 2, game.State.StackItems.Count); Assert.Equal(parent, game.State.StackItems[0]);
            Assert.Equal(copies + 1, game.State.StackItems.Where(s => s.SourceObjectId == "VICTIM")
                .Select(s => s.LastBreathDraw?.InstanceId ?? s.LastBreathRuneCall!.InstanceId).Distinct().Count());
            AssertPrivateBirths(game.State);
            for (var i = 0; i <= copies; i++)
            {
                var top = game.State.StackItems[^1]; Assert.Equal("VICTIM", top.SourceObjectId); Assert.Equal("P1", top.ControllerId);
                if (runeCount > 0)
                {
                    var birth = Assert.IsType<LastBreathRuneCallContext>(top.LastBreathRuneCall);
                    Assert.Equal(TriggerKinds.UnitLastBreathCallRunesThenDraw, birth.Kind); Assert.Equal(victimGeneration, birth.Source.Generation);
                }
                else
                {
                    var birth = Assert.IsType<LastBreathDrawContext>(top.LastBreathDraw);
                    Assert.Equal(discardCount > 0 ? TriggerKinds.UnitLastBreathDiscardDraw : TriggerKinds.UnitLastBreathDrawOne, birth.Kind);
                    Assert.Equal(victimGeneration, birth.Source.Generation);
                }
                var hand = game.State.PlayerZones["P1"].Hand.ToArray(); var deck = game.State.PlayerZones["P1"].MainDeck.ToArray();
                var runes = game.State.PlayerZones["P1"].RuneDeck.ToArray(); prefix = game.Events().Length;
                await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
                Assert.Equal(hand, game.State.PlayerZones["P1"].Hand); Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck);
                Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
                await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
                if (discardCount > 0 && hand.Length >= discardCount)
                {
                    var pending = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
                    Assert.Equal("P1", pending.PlayerId); Assert.Equal(discardCount, pending.RequiredCount); Assert.Equal(drawCount, pending.DrawCount);
                    Assert.Equal(hand, pending.LegalObjectIds); Assert.Equal(victimGeneration, pending.LastBreathDraw!.Source.Generation);
                    Assert.Equal(hand, game.State.PlayerZones["P1"].Hand); Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck);
                    AssertPrivateBirths(game.State);
                    if (i == 0)
                        foreach (var actor in new[] { "P2", "P1" })
                        {
                            var invalid = new ChooseHandCardsCommand(pending.ChoiceId, pending.ChoiceWindow, actor == "P1" ? [] : hand.Take(discardCount).ToArray());
                            var raw = JsonSerializer.SerializeToNode(invalid, Json)!.AsObject();
                            raw["promptId"] = game.PromptFor(actor).PromptId; raw["snapshotTick"] = game.State.Tick;
                            await game.ActRetained(new(actor, "cost-agent-choice-reject-" + actor, JsonSerializer.SerializeToElement(raw, Json)), false);
                            Assert.Equal(parent, Assert.Single(game.State.StackItems, s => s.EmpowerActivation is not null));
                        }
                    await game.Act("P1", new ChooseHandCardsCommand(pending.ChoiceId, pending.ChoiceWindow, hand.Take(discardCount).ToArray()));
                }
                else Assert.Null(game.State.PendingHandChoice);
                var discarded = Math.Min(discardCount, hand.Length);
                Assert.Equal(hand.Skip(discarded).Concat(deck.Take(drawCount)), game.State.PlayerZones["P1"].Hand);
                Assert.Equal(deck.Skip(drawCount), game.State.PlayerZones["P1"].MainDeck);
                var recent = game.Events().Skip(prefix).Select(e => e.Event).ToArray();
                var drawn = Assert.Single(recent, e => e.Kind == "CARD_DRAWN");
                Assert.Equal(drawCount, Convert.ToInt32(drawn.Payload["count"])); Assert.Equal(discarded, recent.Count(e => e.Kind == "CARD_DISCARDED"));
                if (discarded > 0) Assert.True(Array.FindLastIndex(recent, e => e.Kind == "CARD_DISCARDED") < Array.FindIndex(recent, e => e.Kind == "CARD_DRAWN"));
                if (runeCount > 0)
                {
                    var expected = Math.Min(runeCount, runes.Length); var called = Assert.Single(recent, e => e.Kind == "RUNES_CALLED");
                    Assert.Equal(expected, Convert.ToInt32(called.Payload["count"])); Assert.Equal(runes.Skip(expected), game.State.PlayerZones["P1"].RuneDeck);
                    Assert.True(Array.IndexOf(recent, called) < Array.FindIndex(recent, e => e.Kind == "CARD_DRAWN"));
                    foreach (var id in runes.Take(expected)) { Assert.Contains(id, game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects[id].IsExhausted); }
                }
                else Assert.Equal(runes, game.State.PlayerZones["P1"].RuneDeck);
                Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(3, Might(game.State));
                Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
                Assert.Equal(copies - i + 1, game.State.StackItems.Count); Assert.Equal(parent, game.State.StackItems[0]);
            }
            Assert.Empty(game.State.TriggerQueue); Assert.Single(game.State.StackItems); await game.Top();
            Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(5, Might(game.State));
            Assert.Equal(3, game.State.CardObjects["UNIT"].Power); Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            var draws = game.Events().Where(e => e.Event.Kind == "CARD_DRAWN").ToArray(); Assert.Equal(copies + 1, draws.Length);
            Assert.Equal((copies + 1) * drawCount, draws.Sum(e => Convert.ToInt32(e.Event.Payload["count"])));
            Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingHandChoice); await game.Replay();
        }
        finally { await Save(victimNo, copies, held, initial, game); }
    }

    private static MatchState Position(string no, int copies, int mana, int power, int held)
    {
        var state = VenUnitEntryBasicTests.Position("VEN·124"); var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        void Add(string id, string card, string zone)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, card, "P1"); locations[id] = new("P1", zone);
            zones["P1"] = zone switch {
                "BASE" => zones["P1"] with { Base = [..zones["P1"].Base, id] },
                "RUNE_DECK" => zones["P1"] with { RuneDeck = [..zones["P1"].RuneDeck, id] },
                "MAIN_DECK" => zones["P1"] with { MainDeck = [..zones["P1"].MainDeck, id] },
                _ => zones["P1"] with { Hand = [..zones["P1"].Hand, id] } };
        }
        Add("VICTIM", no, "HAND"); for (var i = 0; i < held; i++) Add("HELD-" + i, "OGN·219/298", "HAND");
        for (var i = 0; i < copies; i++) Add("SONG-" + i, "SFD·059/221", "BASE");
        var needed = 3 + mana + copies;
        for (var i = 0; i < 12; i++) Add("COST-RUNE-" + i, power == 1 && i == 0 ? "UNL-R05" : "UNL-R02", i < needed ? "BASE" : "RUNE_DECK");
        for (var i = 4; i <= 12; i++) Add("P1-DRAW-" + i, "OGN·219/298", "MAIN_DECK");
        return state with { RoomId = "VEN124-PAID-LAST-BREATH-" + no, CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
    }
    private static int Might(MatchState state) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Json)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
    private static void AssertPrivateBirths(MatchState state)
    {
        foreach (var view in ResolutionResult.BuildSnapshots(state).Values.Cast<object>().Append(ResolutionResult.BuildSpectatorSnapshot(state)))
        {
            var raw = JsonSerializer.Serialize(view, Json);
            Assert.DoesNotContain("\"lastBreathDraw\"", raw, StringComparison.Ordinal);
            Assert.DoesNotContain("\"lastBreathRuneCall\"", raw, StringComparison.Ordinal);
            Assert.DoesNotContain("\"empowerActivation\"", raw, StringComparison.Ordinal);
        }
    }
    private static async Task Save(string no, int copies, int held, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN124_LAST_BREATH_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); await File.WriteAllTextAsync(Path.Combine(folder, no.Replace('/', '-') + "-copies-" + copies + "-held-" + held + ".json"), JsonSerializer.Serialize(new {
            scope = "Real No124 required destruction with zero pools, exactly12 physical own runes, actual paid normal victim/No124 plays and0/2 paid Song texts. Independent plain draw, compound runes-then-draw and discard-then-draw skills respond/finish before parent Empower. Initial gear/held cards/decks are explicit turn-five material; no forced empowerment/skills/results/source profile, ordinary opening, SQL/network, applicability, complete L2/L3 or whole-card claim. Original per-command hidden/current recovery/dedup/action replay remains.",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
