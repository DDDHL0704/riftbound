using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Game = Riftbound.ConformanceTests.VenEmpowerCanonicalOpeningTests.CanonicalGame;

namespace Riftbound.ConformanceTests;

public sealed class CanonicalDelayedAuraDeathOpeningTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LegalLeonaAndJinxOpeningsKeepTheChosenInverseOrderAndConditionalDeathThroughColdRecovery(bool auraFirst)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var fields = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "poppy-standby").Decklist.Battlefields;
        var ownerCards = Copies("VEN·035", "UNL-156/219", "VEN·130", "SFD·051/221", "OGN·238/298")
            .Concat(Copies("OGN·044/298", "OGN·049/298", "OGN·051/298", "OGN·052/298", "OGN·054/298",
                "OGN·055/298", "OGN·056/298", "OGN·059/298", "OGN·061/298")).Take(40).ToArray();
        var enemyCards = Copies("OGN·190/298", "OGN·009/298", "OGN·030/298")
            .Concat(Copies("OGN·012/298", "OGN·013/298", "OGN·024/298", "OGN·026/298", "OGN·027/298",
                "OGN·029/298", "OGN·039/298", "OGN·171/298", "OGN·180/298", "OGN·183/298", "OGN·185/298")).Take(40).ToArray();
        var decks = new Dictionary<string, OfficialDecklist> {
            ["P1"] = new("OGN·261/298", "OGN·238/298", ownerCards,
                Enumerable.Repeat("UNL-R02", 6).Concat(Enumerable.Repeat("UNL-R04", 6)).ToArray(), fields),
            ["P2"] = new("OGN·251/298", "OGN·030/298", enemyCards,
                Enumerable.Repeat("UNL-R01", 6).Concat(Enumerable.Repeat("UNL-R05", 6)).ToArray(), fields) };
        foreach (var deck in decks.Values)
        {
            var valid = OfficialDeckValidator.Validate(deck, catalog); Assert.True(valid.IsValid, string.Join("; ", valid.Errors));
            Assert.Equal(40, deck.MainDeck.Count); Assert.Equal(12, deck.RuneDeck.Count); Assert.Equal(3, deck.Battlefields.Count);
        }
        var initial = MatchReplayInitialStateBuilder.FromSeats("CANONICAL-DELAYED-AURA-DEATH",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = 424242 };
        var game = new Game(initial);
        try
        {
            foreach (var (player, deck) in decks)
                await game.Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await game.Act("P1", new ReadyCommand()); await game.Act("P2", new ReadyCommand());
            await game.Act(game.State.ActivePlayerId, new MulliganCommand([])); await game.Act(game.State.OpeningSecondActionPlayerId!, new MulliganCommand([]));
            await game.ColdCheckpoint();
            for (var step = 0; ; step++)
            {
                Assert.InRange(step, 0, 79); Assert.Equal(MatchStatuses.InProgress, game.State.Status);
                if (game.State.TurnPlayerId == "P1" && game.Runes("P1").Length == 12 && game.Runes("P2").Length == 12
                    && game.Hand("P1", "VEN·035").Count() == 3 && game.Hand("P1", "UNL-156/219").Count() >= 2
                    && game.Hand("P1", "VEN·130").Any() && game.Hand("P1", "SFD·051/221").Any()
                    && game.Hand("P2", "OGN·190/298").Any() && game.Hand("P2", "OGN·009/298").Any()) break;
                await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            }
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BURNOUT_APPLIED"); await game.ColdCheckpoint();
            await game.Tap("P1"); Assert.Equal(12, game.State.RunePools["P1"].Mana);
            var aura = game.Hand("P1", "VEN·130").First(); await game.Act("P1", new PlayCardCommand(aura, "VEN·130", []));
            var poros = game.Hand("P1", "UNL-156/219").Take(2).ToArray(); var host = poros[0]; var victim = poros[1];
            foreach (var id in poros) await game.Act("P1", new PlayCardCommand(id, "UNL-156/219", []));
            Assert.Equal(1, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects[aura].IsEmpowered);
            var hostGeneration = game.State.CardObjects[host].ObjectGeneration; var victimGeneration = game.State.CardObjects[victim].ObjectGeneration;
            await game.ColdCheckpoint(); await game.Act("P1", new EndTurnCommand());
            await game.Tap("P2"); await Recycle(game, "P2", "UNL-R05");
            var source = game.Hand("P2", "OGN·190/298").First(); await game.Act("P2", new PlayCardCommand(source, "OGN·190/298", []));
            Assert.Equal(9, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            var sourceGeneration = game.State.CardObjects[source].ObjectGeneration;
            await game.ColdCheckpoint(); await game.Act("P2", new EndTurnCommand());
            await game.Tap("P1"); var guardian = game.Hand("P1", "SFD·051/221").First();
            await game.Act("P1", new PlayCardCommand(guardian, "SFD·051/221", [])); await Recycle(game, "P1", "UNL-R02");
            await game.Act("P1", new AssembleEquipmentCommand(guardian, host, ["ASSEMBLE_GREEN"])); await game.Top();
            Assert.Equal(10, game.State.RunePools["P1"].Mana); Assert.Equal(host, game.State.CardObjects[guardian].AttachedToObjectId);
            await game.ColdCheckpoint(); await game.Act("P1", new EndTurnCommand());
            var field = game.State.CardObjects.Values.Single(c => c.CardNo == "OGN·278/298" && c.OwnerId == "P1").ObjectId;
            Assert.False(game.State.CardObjects[source].IsExhausted);
            await game.Act("P2", new MoveUnitCommand(source, "BASE", "BATTLEFIELD:" + field, [])); await FinishWindow(game);
            Assert.Equal("P2", game.State.CardObjects[field].ControllerId); await game.Act("P2", new EndTurnCommand());
            await game.Tap("P1"); var spells = game.Hand("P1", "VEN·035").Take(3).ToArray();
            var targets = new[] { host, victim, aura };
            for (var i = 0; i < 3; i++)
            {
                await Recycle(game, "P1", "UNL-R02");
                await game.Act("P1", new PlayCardCommand(spells[i], "VEN·035", [targets[i]], Mode: "EMPOWER_THEN_REMOVE")); await game.Top();
                Assert.True(game.State.CardObjects[targets[i]].IsEmpowered);
            }
            Assert.Equal(3, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(6, game.Might("P1", host)); Assert.Equal(5, game.Might("P1", victim)); Assert.Equal(7, game.Might("P1", aura));
            await game.ColdCheckpoint();
            await game.Act("P1", new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + field, [], poros));
            if (game.State.FocusPlayerId != "P2") await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
            Assert.Equal("P2", game.State.FocusPlayerId); Assert.Equal(TimingStates.SpellDuelOpen, game.State.TimingState);
            var red = game.Runes("P2").First(id => game.State.CardObjects[id].CardNo == "UNL-R01");
            await game.Act("P2", new TapRuneCommand(red)); await game.Act("P2", new RecycleRuneCommand(red));
            await game.Act("P2", new PlayCardCommand(game.Hand("P2", "OGN·009/298").First(), "OGN·009/298", [source])); await game.Top();
            Assert.Equal(0, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            Assert.Contains(source, game.State.PlayerZones["P2"].Graveyard);
            var area = Assert.IsType<LastBreathBattlefieldDamageContext>(Assert.Single(game.State.StackItems).LastBreathBattlefieldDamage);
            Assert.Equal(new ObjectBinding(source, sourceGeneration), area.Source); Assert.Equal("P2", area.ControllerId); Assert.Equal(field, area.BattlefieldObjectId);
            await game.ColdCheckpoint(); var areaStart = game.Events().Length; await game.Top();
            var hits = game.Events().Skip(areaStart).Where(e => e.Event.Kind == "DAMAGE_APPLIED").ToArray();
            Assert.Equal(2, hits.Length); Assert.All(hits, e => Assert.Equal(4, Convert.ToInt32(e.Event.Payload["damage"])));
            Assert.All(hits, e => Assert.Equal(source, e.Event.Payload.GetValueOrDefault("sourceObjectId")));
            foreach (var id in poros) { Assert.Equal(4, game.State.CardObjects[id].Damage); Assert.Contains("P2", game.State.CardObjects[id].DamageMarkedByPlayerIds!); }
            Assert.Equal(0, game.State.CardObjects[aura].Damage); Assert.Equal(host, game.State.CardObjects[guardian].AttachedToObjectId);
            await FinishWindow(game); Assert.False(game.State.BattleState.IsActive); Assert.Equal("P1", game.State.CardObjects[field].ControllerId);
            Assert.Single(game.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
            Assert.DoesNotContain(game.Events().Skip(areaStart), e => e.Event.Kind == "UNIT_DESTROYED"); await game.ColdCheckpoint();

            var hand = game.State.PlayerZones["P1"].Hand.ToArray(); var deckAtDeath = game.State.PlayerZones["P1"].MainDeck.ToArray();
            Assert.NotEmpty(deckAtDeath); var scores = game.State.PlayerScores.ToDictionary();
            await game.Act("P1", new EndTurnCommand()); Assert.Equal(3, game.State.TriggerQueue.Count);
            var byUnit = game.State.TriggerQueue.ToDictionary(t => Assert.IsType<DelayedEmpowerContext>(t.DelayedEmpower).Unit.ObjectId);
            var order = auraFirst ? new[] { aura, host, victim } : new[] { victim, aura, host };
            await game.ColdCheckpoint();
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: order.Select(id => byUnit[id].TriggerId).ToArray()));
            var top = game.State.StackItems[^1]; var birth = Assert.IsType<DelayedEmpowerContext>(top.DelayedEmpower);
            Assert.Equal(order[0], birth.Unit.ObjectId); Assert.True(birth.CreatedAtTick > 1);
            await game.RejectCoordinatedForgery("legal-delayed-aura-backdated-inverse", game.State with { StackItems = game.State.StackItems
                .Select(i => i == top ? i with { DelayedEmpower = birth with { CreatedAtTick = birth.CreatedAtTick - 1 } } : i).ToArray() });
            var endStart = game.Events().Length; await game.Top();
            Assert.Contains(victim, game.State.PlayerZones["P1"].Graveyard); Assert.Equal(victimGeneration + 1, game.State.CardObjects[victim].ObjectGeneration);
            var first = game.Events().Skip(endStart).ToArray(); var death = Assert.Single(first, e => e.Event.Kind == "UNIT_DESTROYED" && Target(e.Event) == victim);
            if (auraFirst)
            {
                var recalled = Assert.Single(first, e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == host);
                var consumed = Assert.Single(first, e => e.Event.Kind == "EQUIPMENT_DESTROYED" && Target(e.Event) == guardian);
                Assert.True(consumed.Sequence < recalled.Sequence && recalled.Sequence < death.Sequence);
                Assert.Equal(0, game.State.CardObjects[host].Damage); Assert.True(game.State.CardObjects[host].IsExhausted);
                Assert.Contains(host, game.State.PlayerZones["P1"].Base); Assert.Equal(2, game.State.StackItems.Count);
            }
            else
            {
                Assert.Contains(host, game.State.PlayerZones["P1"].Battlefields); Assert.Equal(4, game.State.CardObjects[host].Damage);
                var draw = Assert.IsType<LastBreathDrawContext>(game.State.StackItems[^1].LastBreathDraw);
                Assert.Equal(new ObjectBinding(victim, victimGeneration), draw.Source); Assert.False(draw.SourceWasAlone);
                Assert.Equal(3, game.State.StackItems.Count); await game.ColdCheckpoint(); await game.Top();
                Assert.Equal(hand.Concat(deckAtDeath.Take(1)), game.State.PlayerZones["P1"].Hand);
                Assert.Equal(deckAtDeath.Skip(1), game.State.PlayerZones["P1"].MainDeck);
            }
            await game.ColdCheckpoint();
            for (var i = 1; i < 3; i++)
            {
                var inverse = Assert.IsType<DelayedEmpowerContext>(game.State.StackItems[^1].DelayedEmpower);
                Assert.Equal(order[i], inverse.Unit.ObjectId); if (order[i] == victim) Assert.Equal(victimGeneration, inverse.Unit.Generation);
                await game.Top();
            }
            Assert.Equal("P2", game.State.TurnPlayerId); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); Assert.Null(game.State.EndStep);
            Assert.Contains(host, game.State.PlayerZones["P1"].Base); Assert.Equal(hostGeneration, game.State.CardObjects[host].ObjectGeneration);
            Assert.Equal(0, game.State.CardObjects[host].Damage); Assert.False(game.State.CardObjects[host].IsEmpowered); Assert.False(game.State.CardObjects[aura].IsEmpowered);
            var ending = game.Events().Skip(endStart).ToArray();
            Assert.Single(ending, e => e.Event.Kind == "UNIT_DESTROYED" && Target(e.Event) == victim);
            Assert.DoesNotContain(ending, e => e.Event.Kind == "UNIT_DESTROYED" && Target(e.Event) == host || e.Event.Kind == "DAMAGE_APPLIED");
            Assert.Equal(auraFirst ? 0 : 1, ending.Count(e => e.Event.Kind == "CARD_DRAWN" && Equals(e.Event.Payload.GetValueOrDefault("playerId"), "P1")));
            Assert.Equal(scores, game.State.PlayerScores);
            foreach (var player in new[] { "P1", "P2" })
                Assert.Equal(12, game.State.CardObjects.Values.Count(c => c.OwnerId == player && c.Tags.Contains(CardObjectTags.RuneCard)));
            await game.ColdCheckpoint();
        }
        finally { await game.Save("legal-leona-jinx-delayed-aura-first-" + auraFirst); }
    }

    private static IEnumerable<string> Copies(params string[] cards) => cards.SelectMany(no => Enumerable.Repeat(no, 3));
    private static async Task Recycle(Game game, string player, string no)
        => await game.Act(player, new RecycleRuneCommand(game.Runes(player).First(id => game.State.CardObjects[id].CardNo == no)));
    private static string? Target(GameEvent value) => value.Payload.GetValueOrDefault("targetObjectId") as string;
    private static async Task FinishWindow(Game game)
    {
        for (var i = 0; game.State.BattleState.IsActive || game.State.TimingState != TimingStates.NeutralOpen; i++)
        {
            Assert.InRange(i, 0, 11);
            if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
            else if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else Assert.Fail("The real battlefield window did not expose its next ordinary response.");
        }
    }
}
