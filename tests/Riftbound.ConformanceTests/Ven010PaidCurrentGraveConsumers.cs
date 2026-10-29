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

public sealed class Ven010PaidCurrentGraveConsumers
{
    private const string Curse = "VEN·010";
    private const string Unit = "VEN·013";
    private const string Moon = "UNL-125/219";
    private const string Dispose = "UNL-103/219";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("P1", true)] [InlineData("P2", true)]
    [InlineData("P1", false)] [InlineData("P2", false)]
    public async Task PhysicalTwelveRunesPayForRealGravesAndAReactionChangesOnlyTheResolutionTimeCount(string actor, bool add)
    {
        var initial = Position(actor); var game = new Driver(initial);
        try
        {
            if (actor == "P2") await game.Act("P1", new EndTurnCommand());
            await Sequence(() => game.State, game.Act, game.Top, game.Events, actor, Arena.Host(actor),
                ["CURSE-A", "CURSE-B", "CURSE-C"], "ENEMY-CURSE", "MOON", "DISPOSE", "BF", add,
                command => Ven001PhysicalThresholdConsumerTests.Reject(game, actor, command, "no010-one-mana-short"));
            await game.Replay();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN010_PAID_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, actor + "-" + add + ".json"), JsonSerializer.Serialize(new
                {
                    scope = "Declared midgame hand/deck materials; zero pools/exact12 physical each. Actual No0135mana/turnready/move, enemyNo0102mana andownNo0102mana createbothgraves, nextNo0102mana islowerchain whileactualMoon3mana discardorDisposal2mana recyclechangesowncurrentcount. Basecaptured2, actualdamage4or2 beforelowerSourcejoinsgrave; opponentgrave/handnotcounted. No source/status/resource/history overrides; legalopening separate; L2/L3/fullapplicability/wholecard OPEN.",
                    actor, add, initial, final = game.State, commands = game.Commands(), events = game.Events(), ruleInputs = RuleInputIdentity.Current
                }, Json));
            }
        }
    }

    [Theory]
    [InlineData("P1", true)] [InlineData("P2", true)]
    [InlineData("P1", false)] [InlineData("P2", false)]
    public async Task IndependentlyLegalJinxAndLucianOpeningsUseTheCurrentGraveAfterThePaidReaction(string actor, bool add)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var enemy = Arena.Other(actor);
        OfficialDecklist Deck(string legend, string champion, string rune, string[] additions)
        {
            var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
            var result = Assert.IsType<OfficialDecklist>(builder.Invoke(null, [catalog, catalog.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
                legend, champion, additions, new[] { "OGN·278/298", "OGN·276/298", "OGN·275/298" }, Enumerable.Repeat(rune, 12).ToArray()]));
            var validation = OfficialDeckValidator.Validate(result, catalog); Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
            return result;
        }
        var own = ArrangeGraveReactionDeck(Deck("OGN·251/298", "OGN·030/298", "UNL-R01", [Unit, Unit, Unit, Curse, Curse, Curse, Moon, Moon, Moon]));
        var opposing = Deck("SFD·183/221", "SFD·028/221", "UNL-R04", [Curse, Curse, Curse, Dispose, Dispose, Dispose]);
        var initial = MatchReplayInitialStateBuilder.FromSeats("CANONICAL-010-CURRENT-GRAVE-" + actor + "-" + add,
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = 424242 };
        var game = new CanonicalGame(initial);
        async Task Act(string player, GameCommand command) => await ActCore(player, command,
            checkpoint: command is not (TapRuneCommand or PassPriorityCommand or PassFocusCommand));
        async Task ActCore(string player, GameCommand command, bool checkpoint)
        {
            await game.Act(player, command);
            if (checkpoint) await game.ColdCheckpoint();
            if (game.State.Phase != MatchPhases.TurnStart) return;
            for (var step = 0; step < 12 && game.State.Phase == MatchPhases.TurnStart; step++)
            {
                Assert.Null(game.State.PendingCardChoice); var trigger = Assert.Single(game.State.StackItems);
                Assert.Equal("CAPTURED_BEGINNING_DRAW", trigger.EffectKind); Assert.Equal("OGN·251/298", trigger.CardNo);
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
            Assert.Fail("Required cards must be naturally drawn within the fixed legal deck and bounded turns.");
        }
        try
        {
            foreach (var player in new[] { "P1", "P2" })
            {
                var deck = player == actor ? own : opposing;
                await Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            }
            await Act("P1", new ReadyCommand()); await Act("P2", new ReadyCommand());
            await Act(game.State.ActivePlayerId, new MulliganCommand([])); await Act(game.State.OpeningSecondActionPlayerId!, new MulliganCommand([]));
            await WaitFor(actor, add ? [Unit, Curse, Curse, Curse, Moon] : [Unit, Curse, Curse, Curse]);
            await WaitFor(enemy, add ? [Curse] : [Curse, Dispose]); await WaitFor(actor);
            await game.ColdCheckpoint();
            var unit = game.Hand(actor, Unit).First(); var curses = game.Hand(actor, Curse).Take(3).ToArray();
            var enemyCurse = game.Hand(enemy, Curse).First(); var moon = add ? game.Hand(actor, Moon).First() : "unused-moon";
            var dispose = add ? "unused-disposal" : game.Hand(enemy, Dispose).First();
            var field = Assert.Single(game.State.PlayerZones[actor].Battlefields, id => game.State.CardObjects[id].Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag));
            await Sequence(() => game.State, Act,
                async () => { await Act(game.State.PriorityPlayerId!, new PassPriorityCommand()); await Act(game.State.PriorityPlayerId!, new PassPriorityCommand()); },
                game.Events, actor, unit, curses, enemyCurse, moon, dispose, field, add);
            await game.ColdCheckpoint();
            var forged = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects)
                { [unit] = game.State.CardObjects[unit] with { Damage = 1 } } };
            await game.RejectCoordinatedForgery("invented-damage-after-real-turn-cleanup", forged);
        }
        finally { await game.Save("ven010-paid-current-grave-" + actor + "-" + add); }
    }

    [Theory]
    [InlineData("P1", true)] [InlineData("P2", true)]
    [InlineData("P1", false)] [InlineData("P2", false)]
    public async Task ExplicitFrozenChinaStandardOpeningsPayForTheSameCurrentGraveReactionCases(string actor, bool add)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var enemy = Arena.Other(actor);
        OfficialDecklist Deck(string legend, string champion, string rune, string[] additions)
        {
            var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.NonPublic | BindingFlags.Static)!;
            var result = Assert.IsType<OfficialDecklist>(builder.Invoke(null, [catalog, catalog.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
                legend, champion, additions, new[] { "OGN·278/298", "OGN·277/298", "OGN·279/298" }, Enumerable.Repeat(rune, 12).ToArray()]));
            foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
            {
                var validation = OfficialDeckValidator.Validate(result, catalog, format);
                Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
            }
            return result;
        }
        var own = ArrangeGraveReactionDeck(Deck("OGN·251/298", "OGN·030/298", "UNL-R01", [Unit, Unit, Unit, Curse, Curse, Curse, Moon, Moon, Moon]));
        var opposing = Deck("SFD·183/221", "SFD·028/221", "UNL-R04", [Curse, Curse, Curse, Dispose, Dispose, Dispose]);
        var run = new CopiedDuelInsightOpeningTests.Opening(10010 + (actor == "P1" ? 0 : 2) + (add ? 0 : 1), OfficialDeckFormat.ChinaStandard20260724);
        string[] forgeryErrors = [];
        async Task Act(string player, GameCommand command) => await ActCore(player, command,
            checkpoint: command is not (TapRuneCommand or PassPriorityCommand or PassFocusCommand));
        async Task ActCore(string player, GameCommand command, bool checkpoint)
        {
            await run.Submit(player, command);
            if (checkpoint) await run.Cold();
            if (run.State.Phase != MatchPhases.TurnStart) return;
            for (var step = 0; step < 12 && run.State.Phase == MatchPhases.TurnStart; step++)
            {
                Assert.Null(run.State.PendingCardChoice); var trigger = Assert.Single(run.State.StackItems);
                Assert.Equal("CAPTURED_BEGINNING_DRAW", trigger.EffectKind); Assert.Equal("OGN·251/298", trigger.CardNo);
                await run.Submit(run.State.PriorityPlayerId!, new PassPriorityCommand());
                if (checkpoint) await run.Cold();
            }
            Assert.Equal(MatchPhases.Main, run.State.Phase);
        }
        IEnumerable<string> Hand(string player, string no) => run.State.PlayerZones[player].Hand.Where(id => run.State.CardObjects[id].CardNo == no);
        async Task WaitFor(string player, params string[] cards)
        {
            for (var turn = 0; turn < 80; turn++)
            {
                Assert.Equal(MatchPhases.Main, run.State.Phase);
                if (run.State.TurnPlayerId == player && Runes(run.State, player).Length == 12
                    && cards.GroupBy(no => no).All(g => Hand(player, g.Key).Count() >= g.Count())) return;
                await ActCore(run.State.TurnPlayerId!, new EndTurnCommand(), checkpoint: false);
            }
            Assert.Fail("The explicit frozen China Standard decks must naturally draw the required cards within the bounded turns.");
        }
        try
        {
            foreach (var player in new[] { "P1", "P2" })
            {
                var deck = player == actor ? own : opposing;
                await Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            }
            await Act("P1", new ReadyCommand()); await Act("P2", new ReadyCommand());
            await Act(run.State.ActivePlayerId, new MulliganCommand([])); await Act(run.State.OpeningSecondActionPlayerId!, new MulliganCommand([]));
            await WaitFor(actor, add ? [Unit, Curse, Curse, Curse, Moon] : [Unit, Curse, Curse, Curse]);
            await WaitFor(enemy, add ? [Curse] : [Curse, Dispose]); await WaitFor(actor);
            await run.Cold();
            var unit = Hand(actor, Unit).First(); var curses = Hand(actor, Curse).Take(3).ToArray();
            var enemyCurse = Hand(enemy, Curse).First(); var moon = add ? Hand(actor, Moon).First() : "unused-moon";
            var dispose = add ? "unused-disposal" : Hand(enemy, Dispose).First();
            var field = Assert.Single(run.State.PlayerZones[actor].Battlefields, id => run.State.CardObjects[id].Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag));
            await Sequence(() => run.State, Act,
                async () => { await Act(run.State.PriorityPlayerId!, new PassPriorityCommand()); await Act(run.State.PriorityPlayerId!, new PassPriorityCommand()); },
                () => run.Frame().Events.ToArray(), actor, unit, curses, enemyCurse, moon, dispose, field, add);
            await run.Cold(); var original = run.Frame();
            Assert.Empty(original.ValidationErrors);
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(original, new CoreRuleEngine(), default));
            var forged = run.State with { CardObjects = new Dictionary<string, CardObjectState>(run.State.CardObjects)
                { [unit] = run.State.CardObjects[unit] with { Damage = 1 } } };
            var forgedFrame = run.Frame(forged); Assert.Empty(forgedFrame.ValidationErrors);
            forgeryErrors = (await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forgedFrame, new CoreRuleEngine(), default)).ToArray();
            Assert.NotEmpty(forgeryErrors);
            Assert.Contains(forgeryErrors, e => e.Contains("replayed final state hash", StringComparison.Ordinal));
            await run.Cold();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN010_STANDARD_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, actor + "-" + add + ".json"), JsonSerializer.Serialize(new
                {
                    scope = "Explicit frozen CN-STANDARD-2026-07-24 native session, blank seed0 40/12/3 submitted decks; real draws/channeling and unchanged paid current-grave sequence. Full original histories and JSON native Restore/private snapshot/next-command comparisons. New format rows are additional; original CoreRules rows and stronger registry/dedup recovery assertions remain unchanged. Current full source applicability/latest L2/L3/whole-card OPEN.",
                    format = ChinaStandardBanList.Version, actor, add, initial = run.Initial, frame = run.State.Tick == 0 ? null : run.Frame(), run.ColdCount, run.OriginalNextMatches, forgeryErrors
                }, Json));
            }
        }
    }

    private static OfficialDecklist ArrangeGraveReactionDeck(OfficialDecklist deck)
    {
        // Submit a fixed legal order with enough cards left for Moon's two draws;
        // the normal opening still shuffles the deck and draws naturally.
        var cards = deck.MainDeck.ToArray();
        Assert.Equal(deck.ChampionCardNo, cards[0]);
        Assert.Equal(new[] { Curse, Curse, Curse }, cards[4..7]);
        foreach (var (from, to) in new[] { (4, 11), (5, 20), (6, 21) })
            (cards[from], cards[to]) = (cards[to], cards[from]);
        return deck with { MainDeck = cards };
    }

    private static async Task Sequence(Func<MatchState> state, Func<string, GameCommand, Task> act, Func<Task> top, Func<RecoveredEvent[]> events,
        string actor, string unit, string[] curses, string enemyCurse, string moon, string dispose, string field, bool add,
        Func<PlayCardCommand, Task>? rejectOneShort = null)
    {
        var enemy = Arena.Other(actor); Common.AssertTwelveEach(state());
        foreach (var player in new[] { actor, enemy }) { Assert.Equal(0, state().RunePools[player].Mana); Assert.Equal(0, state().RunePools[player].TotalPower); Assert.Empty(Graves(state(), player)); }
        async Task Tap(string player, int count)
        {
            var ids = Runes(state(), player).Where(id => !state().CardObjects[id].IsExhausted).Take(count).ToArray(); Assert.Equal(count, ids.Length);
            foreach (var batch in ids.Chunk(12)) await act(player, new TapRuneCommand(batch[0], batch));
        }
        async Task Cast(string player, string source, string no, string[] targets, int mana, string mode = "")
        {
            await Tap(player, mana); var prefix = events().Length; await act(player, new PlayCardCommand(source, no, targets, mode));
            Arena.Cost(events().Skip(prefix), mana, 0); Assert.Equal(0, state().RunePools[player].Mana); Assert.Equal(0, state().RunePools[player].TotalPower);
        }
        void Damage(string source, int amount)
        {
            var hit = Assert.Single(events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), source));
            Assert.Equal(amount, hit.Event.Payload["damage"]); Assert.Equal(amount, state().CardObjects[unit].Damage);
        }
        await Cast(actor, unit, Unit, [], 5); Assert.True(state().CardObjects[unit].IsExhausted); Assert.Equal(5, Arena.Might(state(), actor, unit));
        var generation = state().CardObjects[unit].ObjectGeneration;
        await act(actor, new EndTurnCommand()); await act(enemy, new EndTurnCommand()); Assert.False(state().CardObjects[unit].IsExhausted);
        await act(actor, new MoveUnitCommand(unit, "BASE", "BATTLEFIELD:" + field, [])); await Common.CompleteMovementWindow(state, act);
        await act(actor, new EndTurnCommand()); await Cast(enemy, enemyCurse, Curse, [unit], 2); await top(); Damage(enemyCurse, 2);
        Assert.Equal(new[] { enemyCurse }, Graves(state(), enemy)); Assert.Empty(Graves(state(), actor));
        await act(enemy, new EndTurnCommand()); Assert.Equal(0, state().CardObjects[unit].Damage);
        await Cast(actor, curses[0], Curse, [unit], 2); await top(); Damage(curses[0], 2); Assert.Equal(new[] { curses[0] }, Graves(state(), actor));
        await act(actor, new EndTurnCommand()); await act(enemy, new EndTurnCommand()); Assert.Equal(0, state().CardObjects[unit].Damage);
        var lower = new PlayCardCommand(curses[1], Curse, [unit]); await Tap(actor, 1);
        if (rejectOneShort is not null) await rejectOneShort(lower);
        await Tap(actor, 1); var prefix = events().Length; await act(actor, lower); Arena.Cost(events().Skip(prefix), 2, 0);
        Assert.Equal(0, state().RunePools[actor].Mana); var captured = Assert.Single(state().StackItems); Assert.Equal(2, captured.DamageAmount);
        Assert.Equal(new[] { curses[0] }, Graves(state(), actor)); Assert.Contains(curses[2], state().PlayerZones[actor].Hand);
        var beforeRecycledGeneration = state().CardObjects[curses[0]].ObjectGeneration;
        if (add)
        {
            Assert.True(state().PlayerZones[actor].MainDeck.Count >= 2, "This discard scenario requires two draws without burnout.");
            await Cast(actor, moon, Moon, [curses[2]], 3); Assert.Contains(curses[2], state().PlayerZones[actor].Hand);
            Assert.Equal(new[] { curses[0] }, Graves(state(), actor)); var hand = state().PlayerZones[actor].Hand.Count;
            await top(); Assert.Contains(curses[2], state().PlayerZones[actor].Graveyard);
            Assert.Equal(hand + 1, state().PlayerZones[actor].Hand.Count); Assert.Equal(2, Graves(state(), actor).Length);
        }
        else
        {
            await act(actor, new PassPriorityCommand()); await Cast(enemy, dispose, Dispose, [curses[0]], 2, "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3");
            Assert.Contains(curses[0], state().PlayerZones[actor].Graveyard); await top(); Assert.Empty(Graves(state(), actor));
            Assert.Contains(curses[0], state().PlayerZones[actor].MainDeck); Assert.Equal(beforeRecycledGeneration + 1, state().CardObjects[curses[0]].ObjectGeneration);
            Assert.Contains(curses[2], state().PlayerZones[actor].Hand);
        }
        Assert.Equal(captured.StackItemId, Assert.Single(state().StackItems).StackItemId); Assert.Equal(2, state().StackItems.Single().DamageAmount);
        Assert.Equal(0, state().CardObjects[unit].Damage); Assert.Equal(new[] { enemyCurse }, Graves(state(), enemy));
        await top(); Damage(curses[1], add ? 4 : 2); Assert.Equal(add ? 3 : 1, Graves(state(), actor).Length);
        Assert.Equal(generation, state().CardObjects[unit].ObjectGeneration); Assert.Equal("BATTLEFIELD", state().ObjectLocations[unit].Zone);
        Assert.Equal(5, Arena.Might(state(), actor, unit)); Assert.False(state().CardObjects[unit].IsEmpowered);
        await act(actor, new EndTurnCommand()); Assert.Equal(0, state().CardObjects[unit].Damage);
        Assert.Equal(generation, state().CardObjects[unit].ObjectGeneration); Assert.Equal("BATTLEFIELD", state().ObjectLocations[unit].Zone);
        Common.AssertTwelveEach(state()); Assert.Equal(MatchStatuses.InProgress, state().Status);
    }

    private static string[] Runes(MatchState state, string player) => state.PlayerZones[player].Base.Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).ToArray();
    private static string[] Graves(MatchState state, string player) => state.PlayerZones[player].Graveyard.Where(id => state.CardObjects[id].CardNo == Curse).ToArray();
    private static CardObjectState Printed(string id, string no, string owner) => OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner);
    private static MatchState Position(string actor)
    {
        var state = Arena.Position(Unit); var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        foreach (var player in new[] { "P1", "P2" })
        {
            for (var i = 0; i < 12; i++) cards[Arena.Rune(player, i)] = Printed(Arena.Rune(player, i), player == actor ? "UNL-R01" : "UNL-R04", player);
            for (var i = 4; i <= 6; i++)
            {
                var id = player + "-DRAW-" + i; cards[id] = Printed(id, "OGN·219/298", player); locations[id] = new(player, "MAIN_DECK");
                zones[player] = zones[player] with { MainDeck = [..zones[player].MainDeck, id] };
            }
        }
        foreach (var (id, no, player) in new[] { ("CURSE-A", Curse, actor), ("CURSE-B", Curse, actor), ("CURSE-C", Curse, actor),
            ("MOON", Moon, actor), ("ENEMY-CURSE", Curse, Arena.Other(actor)), ("DISPOSE", Dispose, Arena.Other(actor)) })
        {
            cards[id] = Printed(id, no, player); locations[id] = new(player, "HAND"); zones[player] = zones[player] with { Hand = [..zones[player].Hand, id] };
        }
        return state with { RoomId = "PHYSICAL-010-CURRENT-GRAVE", CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }
}
