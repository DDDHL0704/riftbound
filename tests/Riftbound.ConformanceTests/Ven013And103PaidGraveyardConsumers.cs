using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Arena = Riftbound.ConformanceTests.Ven093And122PhysicalSelfConsumers;
using Common = Riftbound.ConformanceTests.Ven129PhysicalCombatConsumerTests;
using CanonicalGame = Riftbound.ConformanceTests.VenEmpowerCanonicalOpeningTests.CanonicalGame;

namespace Riftbound.ConformanceTests;

public sealed class Ven013And103PaidGraveyardConsumers
{
    private const string Assassin = "VEN·013";
    private const string Return = "VEN·103";
    private const string Strike = "VEN·008";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("P1", 0)] [InlineData("P2", 0)]
    [InlineData("P1", 1)] [InlineData("P2", 1)]
    [InlineData("P1", 2)] [InlineData("P2", 2)]
    public async Task TwelvePhysicalRunesPayRealEntryDiscardLethalAndZeroOneOrTwoOwnerReturns(string actor, int selected)
    {
        var initial = Arena.Position(Assassin); var cards = initial.CardObjects.ToDictionary();
        foreach (var player in new[] { "P1", "P2" }) for (var i = 0; i < 12; i++)
            cards[Arena.Rune(player, i)] = Printed(Arena.Rune(player, i), i < 6 ? "UNL-R01" : "UNL-R05", player);
        initial = initial with { RoomId = "PAID-013-103-GRAVEYARD", CardObjects = cards };
        initial = Put(Put(Put(Put(initial, "SECOND", Assassin, actor), "THIRD", Assassin, actor), "RETURN", Return, actor), "STRIKE", Strike, Arena.Other(actor));
        var locations = initial.ObjectLocations.ToDictionary(); var zones = initial.PlayerZones.ToDictionary(); cards = initial.CardObjects.ToDictionary();
        foreach (var player in new[] { "P1", "P2" }) for (var i = 4; i <= 6; i++)
        {
            var id = player + "-DRAW-" + i; cards[id] = Printed(id, "OGN·219/298", player); locations[id] = new(player, "MAIN_DECK");
            zones[player] = zones[player] with { MainDeck = [..zones[player].MainDeck, id] };
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
        var game = new Driver(initial);
        try
        {
            if (actor == "P2") await game.Act("P1", new EndTurnCommand());
            await Sequence(() => game.State, game.Act, game.Top, game.Events, actor, Arena.Host(actor), "SECOND", "THIRD", Arena.Host(Arena.Other(actor)), "STRIKE", "RETURN", "BF", selected,
                async command =>
                {
                    await Ven001PhysicalThresholdConsumerTests.Reject(game, actor, command, "graveyard-missing-purple");
                    var red = Runes(game.State, actor).First(id => game.State.CardObjects[id].Tags.Contains("COLOR:" + RuneTrait.Red));
                    await game.Act(actor, new RecycleRuneCommand(red));
                    await Ven001PhysicalThresholdConsumerTests.Reject(game, actor, command, "graveyard-wrong-red");
                    Assert.Equal(1, game.State.RunePools[actor].PowerByTrait[RuneTrait.Red]);
                });
            await game.Replay();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN013103_PAID_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, actor + "-" + selected + ".json"), JsonSerializer.Serialize(new
                {
                    scope = "Declared midgame hand materials; zero pools and exactly12 physical each, real5mana entry/3mana discard5damage/5mana ready entry/3mana purple1 return/5mana new entry. Both seats, all0/1/2 target rows; no source/role/status/resource/history override. Legalopening separate; L2/L3/applicability/wholecard OPEN.",
                    actor, selected, initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current
                }, Json));
            }
        }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task LegalRedPurpleOpeningCreatesBothGravesThenReturnsEachOwnerAndReevaluatesReadyEntry(string actor)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var deck = Assert.IsType<OfficialDecklist>(builder.Invoke(null, [catalog, catalog.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "OGN·251/298", "OGN·030/298", new[] { Assassin, Assassin, Assassin, Return, Return, Return, Strike, Strike, Strike },
            new[] { "OGN·278/298", "OGN·276/298", "OGN·275/298" }, Enumerable.Repeat("UNL-R01", 6).Concat(Enumerable.Repeat("UNL-R05", 6)).ToArray()]));
        var validation = OfficialDeckValidator.Validate(deck, catalog); Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        // Preserve the first player and battlefields; both seats naturally draw their
        // required cards by the time the twelve physical runes have been channeled.
        var initial = MatchReplayInitialStateBuilder.FromSeats("CANONICAL-013-103-" + actor,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = 46574 };
        var game = new CanonicalGame(initial);
        async Task Act(string player, GameCommand command) => await ActCore(player, command,
            checkpoint: command is PlayCardCommand or MoveUnitCommand);
        async Task ActCore(string player, GameCommand command, bool checkpoint)
        {
            await game.Act(player, command);
            if (checkpoint) await game.ColdCheckpoint();
            if (game.State.Phase != MatchPhases.TurnStart) return;
            for (var step = 0; step < 12 && game.State.Phase == MatchPhases.TurnStart; step++)
            {
                Assert.Null(game.State.PendingCardChoice);
                var trigger = Assert.Single(game.State.StackItems);
                Assert.Equal("CAPTURED_BEGINNING_DRAW", trigger.EffectKind);
                Assert.Equal("OGN·251/298", trigger.CardNo);
                await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
                if (checkpoint) await game.ColdCheckpoint();
            }
            Assert.Equal(MatchPhases.Main, game.State.Phase);
        }
        async Task WaitFor(string player, params string[] cards)
        {
            for (var turn = 0; turn < 80; turn++)
            {
                Assert.Equal(MatchPhases.Main, game.State.Phase);
                if (game.State.TurnPlayerId == player && game.Runes(player).Length == 12
                    && cards.GroupBy(no => no).All(g => game.Hand(player, g.Key).Count() >= g.Count())) return;
                await ActCore(game.State.TurnPlayerId!, new EndTurnCommand(), checkpoint: false);
            }
            Assert.Fail("The fixed legal deck must naturally draw the required cards within the bounded turns.");
        }
        try
        {
            // Audit setup and natural draws at stage boundaries; keep cold recovery
            // on paid plays and movement, then validate the complete final journal.
            foreach (var player in new[] { "P1", "P2" })
                await Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await Act("P1", new ReadyCommand()); await Act("P2", new ReadyCommand());
            await Act(game.State.ActivePlayerId, new MulliganCommand([]));
            await Act(game.State.OpeningSecondActionPlayerId!, new MulliganCommand([]));
            await game.ColdCheckpoint();
            var enemy = Arena.Other(actor);
            await WaitFor(actor, Assassin, Assassin, Return); await game.ColdCheckpoint();
            await WaitFor(enemy, Assassin, Strike); await game.ColdCheckpoint();
            await WaitFor(actor); await game.ColdCheckpoint();
            var units = game.Hand(actor, Assassin).Take(2).ToArray();
            var food = game.Hand(enemy, Assassin).First(); var spell = game.Hand(actor, Return).First();
            var strike = game.Hand(enemy, Strike).First();
            var field = Assert.Single(game.State.PlayerZones[actor].Battlefields, id => game.State.CardObjects[id].Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag));
            await Sequence(() => game.State, Act,
                async () =>
                {
                    while (game.State.StackItems.Count > 0) await Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
                    // Recover both the lethal destruction and the later hidden hand returns
                    // before executing any subsequent turn or play commands.
                    await game.ColdCheckpoint();
                },
                game.Events, actor, units[0], units[1], units[0], food, strike, spell, field, 2);
            await game.ColdCheckpoint();
        }
        finally { await game.Save("ven013-103-submitted-paid-graveyard-" + actor); }
    }

    private static async Task Sequence(Func<MatchState> state, Func<string, GameCommand, Task> act, Func<Task> top, Func<RecoveredEvent[]> events,
        string actor, string first, string second, string third, string food, string strike, string spell, string field, int selected,
        Func<PlayCardCommand, Task>? rejectMissingPurple = null)
    {
        var enemy = Arena.Other(actor); Common.AssertTwelveEach(state());
        foreach (var player in new[] { actor, enemy }) { Assert.Equal(0, state().RunePools[player].Mana); Assert.Equal(0, state().RunePools[player].TotalPower); }
        Assert.DoesNotContain(state().PlayerZones[actor].Graveyard, id => state().CardObjects[id].CardNo == Assassin);
        async Task Tap(string player, int count)
        {
            var runes = Runes(state(), player).Where(id => !state().CardObjects[id].IsExhausted).Take(count).ToArray();
            Assert.Equal(count, runes.Length); await act(player, new TapRuneCommand(runes[0], runes));
        }
        async Task Play(string source, bool exhausted)
        {
            await Tap(actor, 5); var mana = state().RunePools[actor].Mana; var prefix = events().Length;
            await act(actor, new PlayCardCommand(source, Assassin, []));
            Assert.Equal(mana - 5, state().RunePools[actor].Mana); Arena.Cost(events().Skip(prefix), 5, 0);
            var unit = state().CardObjects[source]; Assert.Equal(5, unit.Power); Assert.Equal(0, unit.Damage);
            Assert.Equal(exhausted, unit.IsExhausted); Assert.Equal(actor, unit.OwnerId); Assert.Equal(actor, unit.ControllerId);
            Assert.Equal("BASE", state().ObjectLocations[source].Zone); Assert.False(unit.IsEmpowered);
        }
        await Play(first, true); var enteredGeneration = state().CardObjects[first].ObjectGeneration;
        await act(actor, new EndTurnCommand()); await act(enemy, new EndTurnCommand());
        Assert.False(state().CardObjects[first].IsExhausted);
        await act(actor, new MoveUnitCommand(first, "BASE", "BATTLEFIELD:" + field, []));
        await Common.CompleteMovementWindow(state, act);
        Assert.Equal(5, Arena.Might(state(), actor, first)); Assert.Equal(enteredGeneration, state().CardObjects[first].ObjectGeneration);
        await act(actor, new EndTurnCommand()); await Tap(enemy, 3);
        var foodGeneration = state().CardObjects[food].ObjectGeneration; var strikePrefix = events().Length;
        await act(enemy, new PlayCardCommand(strike, Strike, [first], OptionalCosts: ["DISCARD_HAND_CARD:" + food]));
        Arena.Cost(events().Skip(strikePrefix), 3, 0); Assert.Equal(0, state().RunePools[enemy].Mana); Assert.Equal(0, state().RunePools[enemy].TotalPower);
        Assert.Contains(food, state().PlayerZones[enemy].Graveyard); Assert.Equal(foodGeneration + 1, state().CardObjects[food].ObjectGeneration);
        Assert.Equal("BATTLEFIELD", state().ObjectLocations[first].Zone); Assert.Equal(0, state().CardObjects[first].Damage);
        await top(); var damage = Assert.Single(events().Skip(strikePrefix), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), strike));
        Assert.Equal(5, damage.Event.Payload["damage"]); Assert.Contains(first, state().PlayerZones[actor].Graveyard);
        Assert.Equal(enteredGeneration + 1, state().CardObjects[first].ObjectGeneration);
        await act(enemy, new EndTurnCommand()); await Play(second, false);
        Assert.Contains(first, state().PlayerZones[actor].Graveyard);
        var firstGraveGeneration = state().CardObjects[first].ObjectGeneration; var foodGraveGeneration = state().CardObjects[food].ObjectGeneration;
        await Tap(actor, 3); string[] targets = selected switch { 0 => [], 1 => [first], _ => [first, food] };
        var command = new PlayCardCommand(spell, Return, targets);
        if (rejectMissingPurple is not null) await rejectMissingPurple(command);
        var purple = Runes(state(), actor).First(id => state().CardObjects[id].Tags.Contains("COLOR:" + RuneTrait.Purple));
        await act(actor, new RecycleRuneCommand(purple));
        var priorMana = state().RunePools[actor].Mana; var priorPower = state().RunePools[actor].TotalPower; var returnPrefix = events().Length;
        await act(actor, command); Arena.Cost(events().Skip(returnPrefix), 3, 1); Assert.Equal(priorMana - 3, state().RunePools[actor].Mana);
        Assert.Equal(priorPower - 1, state().RunePools[actor].TotalPower);
        Assert.Contains(first, state().PlayerZones[actor].Graveyard); Assert.Contains(food, state().PlayerZones[enemy].Graveyard);
        await top(); Assert.Contains(spell, state().PlayerZones[actor].Graveyard);
        if (selected >= 1) { Returned(state(), actor, first, firstGraveGeneration + 1); HiddenFromOther(state(), actor, first); }
        else Assert.Contains(first, state().PlayerZones[actor].Graveyard);
        if (selected == 2) { Returned(state(), enemy, food, foodGraveGeneration + 1); HiddenFromOther(state(), enemy, food); }
        else Assert.Contains(food, state().PlayerZones[enemy].Graveyard);
        Assert.False(state().CardObjects[second].IsExhausted);
        Assert.Equal(selected == 0, state().PlayerZones[actor].Graveyard.Any(id => state().CardObjects[id].CardNo == Assassin));
        await act(actor, new EndTurnCommand()); await act(enemy, new EndTurnCommand());
        var replayed = selected == 0 ? third : first; var before = state().CardObjects[replayed].ObjectGeneration;
        await Play(replayed, selected > 0); Assert.Equal(before + 1, state().CardObjects[replayed].ObjectGeneration);
        Assert.False(state().CardObjects[second].IsExhausted); Common.AssertTwelveEach(state());
    }

    private static string[] Runes(MatchState state, string player) => state.PlayerZones[player].Base.Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).ToArray();
    private static CardObjectState Printed(string id, string no, string owner) => OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
    private static MatchState Put(MatchState state, string id, string no, string owner) => state with
    {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, "HAND") },
        PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == owner ? p.Value with { Hand = [..p.Value.Hand, id] } : p.Value)
    };
    private static void Returned(MatchState state, string owner, string id, long generation)
    {
        Assert.Equal(new ObjectLocationState(owner, "HAND"), state.ObjectLocations[id]); Assert.Contains(id, state.PlayerZones[owner].Hand);
        Assert.Equal(owner, state.CardObjects[id].OwnerId); Assert.Equal(owner, state.CardObjects[id].ControllerId);
        Assert.Equal(generation, state.CardObjects[id].ObjectGeneration); Assert.False(state.CardObjects[id].IsEmpowered);
    }
    private static void HiddenFromOther(MatchState state, string owner, string id)
    {
        foreach (var snapshot in new[] { ResolutionResult.BuildSnapshots(state)[Arena.Other(owner)], ResolutionResult.BuildSpectatorSnapshot(state) })
            Assert.False(JsonSerializer.SerializeToElement(snapshot, Json).GetProperty("players").GetProperty(owner).GetProperty("objects").TryGetProperty(id, out _));
        Assert.Equal(Assassin, JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[owner], Json)
            .GetProperty("players").GetProperty(owner).GetProperty("objects").GetProperty(id).GetProperty("cardNo").GetString());
    }
}
