using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Game = Riftbound.ConformanceTests.VenEmpowerCanonicalOpeningTests.CanonicalGame;

namespace Riftbound.ConformanceTests;

public sealed partial class CanonicalDistributedGuardianOpeningTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EarnedHoldPointsAndNaturalDeckExhaustionSeparateDeathDrawVictoryFromLaterBattleConquest(bool winOnDraw)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var templates = PreconstructedDeckCatalog.Build(catalog);
        var fields = templates.Single(d => d.Id == "poppy-standby").Decklist.Battlefields;
        var owner = templates.Single(d => d.Id == "lillia-lowcurve").Decklist; var wanted = new[] { "OGN·096/298", "SFD·051/221" };
        var additions = Copies(wanted).ToArray(); var main = owner.MainDeck.Where(no => !wanted.Contains(no)).ToList();
        while (main.Count > 40 - additions.Length) main.RemoveAt(main.FindIndex(no => no != owner.ChampionCardNo));
        main.InsertRange(0, additions);
        var defenderDeck = owner with { MainDeck = main, RuneDeck = Enumerable.Repeat("UNL-R02", 12).ToArray(), Battlefields = fields };
        var attackerMain = Copies("OGN·041/298", "OGN·175/298", "OGN·004/298", "OGN·030/298")
            .Concat(Copies("OGN·012/298", "OGN·013/298", "OGN·024/298", "OGN·026/298", "OGN·027/298",
                "OGN·029/298", "OGN·039/298", "OGN·171/298", "OGN·180/298", "OGN·183/298")).Take(40).ToArray();
        var attackerDeck = new OfficialDecklist("OGN·251/298", "OGN·030/298", attackerMain,
            Enumerable.Repeat("UNL-R01", 6).Concat(Enumerable.Repeat("UNL-R05", 6)).ToArray(), fields);
        var decks = new Dictionary<string, OfficialDecklist> { ["P1"] = defenderDeck, ["P2"] = attackerDeck };
        foreach (var deck in decks.Values)
        {
            var valid = OfficialDeckValidator.Validate(deck, catalog); Assert.True(valid.IsValid, string.Join("; ", valid.Errors));
            Assert.Equal(40, deck.MainDeck.Count); Assert.Equal(12, deck.RuneDeck.Count); Assert.Equal(3, deck.Battlefields.Count);
        }
        var initial = MatchReplayInitialStateBuilder.FromSeats("CANONICAL-BURNOUT-BATTLE-CUTOFF",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = 424242 };
        var game = new Game(initial); var directProbes = new List<object>();
        try
        {
            foreach (var (player, deck) in decks)
                await game.Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await game.Act("P1", new ReadyCommand()); await game.Act("P2", new ReadyCommand());
            await game.Act(game.State.ActivePlayerId, new MulliganCommand([])); await game.Act(game.State.OpeningSecondActionPlayerId!, new MulliganCommand([]));
            await game.ColdCheckpoint();
            for (var i = 0; ; i++)
            {
                await Beginning(game); Assert.InRange(i, 0, 69); Assert.Equal(MatchStatuses.InProgress, game.State.Status);
                if (game.State.TurnPlayerId == "P1" && game.Runes("P1").Length == 12 && game.Runes("P2").Length == 12
                    && game.Hand("P1", "OGN·096/298").Count() >= 2 && game.Hand("P1", "SFD·051/221").Count() >= 2
                    && new[] { "OGN·041/298", "OGN·175/298", "OGN·004/298" }.All(no => game.Hand("P2", no).Any())) break;
                await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            }
            Assert.True(game.State.PlayerZones["P1"].MainDeck.Count > 6);
            await game.Tap("P1", 8);
            var sentinels = game.Hand("P1", "OGN·096/298").Take(2).ToArray(); var host = sentinels[0]; var victim = sentinels[1];
            foreach (var id in sentinels) await game.Act("P1", new PlayCardCommand(id, "OGN·096/298", []));
            var victimGeneration = game.State.CardObjects[victim].ObjectGeneration;
            var guardians = game.Hand("P1", "SFD·051/221").Take(2).ToArray();
            foreach (var id in guardians)
            {
                await game.Act("P1", new PlayCardCommand(id, "SFD·051/221", [])); await Recycle(game, "P1", "UNL-R02");
                await game.Act("P1", new AssembleEquipmentCommand(id, host, ["ASSEMBLE_GREEN"])); await game.Top();
            }
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Act("P1", new EndTurnCommand()); await Beginning(game); await game.Tap("P2");
            await Recycle(game, "P2", "UNL-R01"); await Recycle(game, "P2", "UNL-R01");
            var source = game.Hand("P2", "OGN·041/298").First(); await game.Act("P2", new PlayCardCommand(source, "OGN·041/298", []));
            Assert.Equal(2, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            await game.Act("P2", new EndTurnCommand()); await Beginning(game); await game.Act("P1", new EndTurnCommand()); await Beginning(game);
            await game.Tap("P2", 4);
            var holder = game.Hand("P2", "OGN·175/298").First(); await game.Act("P2", new PlayCardCommand(holder, "OGN·175/298", []));
            var recyclableSpell = game.Hand("P2", "OGN·004/298").First();
            await game.Act("P2", new PlayCardCommand(recyclableSpell, "OGN·004/298", [source])); await game.Top();
            Assert.Equal(0, game.State.RunePools["P2"].Mana); Assert.Equal(new[] { recyclableSpell }, game.State.PlayerZones["P2"].Graveyard);
            // The opening checkpoint above and the later
            // post-exhaustion/burnout checkpoint cover this same accepted
            // setup history. Keep the command assertions here, then recover
            // once after the held-point phase instead of replaying the growing
            // journal at this intermediate boundary.
            var holdWindow = winOnDraw ? 6 : 4;
            for (var i = 0; ; i++)
            {
                await Beginning(game); Assert.InRange(i, 0, 79); Assert.Equal(MatchStatuses.InProgress, game.State.Status);
                if (game.State.TurnPlayerId == "P2" && game.State.PlayerZones["P1"].MainDeck.Count == holdWindow) break;
                await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            }
            Assert.False(game.State.CardObjects[holder].IsExhausted); Assert.Equal(9, game.Might("P2", source));
            var home = game.State.CardObjects.Values.Single(c => c.OwnerId == "P2" && c.CardNo == "OGN·275/298").ObjectId;
            var field = game.State.CardObjects.Values.Single(c => c.OwnerId == "P1" && c.CardNo == "OGN·278/298").ObjectId;
            await game.Act("P2", new MoveUnitCommand(holder, "BASE", "BATTLEFIELD:" + home, [])); await Focus(game);
            Assert.Equal(1, game.State.PlayerScores["P2"]);
            // The next checkpoint after natural exhaustion replays this held-point
            // history, so one restore covers the whole stage.
            for (var i = 0; ; i++)
            {
                await Beginning(game); Assert.InRange(i, 0, 19); Assert.Equal(MatchStatuses.InProgress, game.State.Status);
                if (game.State.TurnPlayerId == "P1" && game.State.PlayerZones["P1"].MainDeck.Count == 0) break;
                await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            }
            Assert.Equal(holdWindow, game.State.PlayerScores["P2"]); Assert.Equal(0, game.State.PlayerScores["P1"]);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BURNOUT_APPLIED");
            await game.Act("P1", new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + field, [], sentinels)); await Focus(game);
            Assert.Equal(1, game.State.PlayerScores["P1"]);
            await game.Act("P1", new EndTurnCommand()); await Beginning(game);
            Assert.Equal(holdWindow + 1, game.State.PlayerScores["P2"]); Assert.Equal(2, game.State.PlayerScores["P1"]);
            Assert.Contains(recyclableSpell, game.State.PlayerZones["P2"].Hand); Assert.Empty(game.State.PlayerZones["P2"].Graveyard);
            Assert.Single(game.Events(), e => e.Event.Kind == "BURNOUT_APPLIED");
            Assert.Equal(9, game.Might("P2", source)); Assert.Equal(3, game.Might("P1", host)); await game.ColdCheckpoint();
            await game.Act("P2", new MoveUnitCommand(source, "BASE", "BATTLEFIELD:" + field, []));
            var confirmation = game.State.PendingCardChoice!; Assert.Equal("TRIGGER_CONFIRMATION", confirmation.ChoiceWindow);
            await game.Act("P2", new ChooseCardsCommand(confirmation.ChoiceId, confirmation.ChoiceWindow, sentinels)); await game.Top();
            var allocation = game.State.PendingCardChoice!; Assert.Equal("EFFECT_DAMAGE_DISTRIBUTION", allocation.ChoiceWindow);
            await game.ColdCheckpoint(); var prefix = game.Events().Length;
            await game.Act("P2", new AssignEffectDamageCommand(allocation.ResolvingStackItemId!, [
                new CombatDamageAssignmentDto(source, host, 3), new CombatDamageAssignmentDto(source, victim, 2)]));
            var replacement = game.State.PendingRuleChoice!.Request; Assert.Equal(2, replacement.Options.Count);
            var chosen = Assert.Single(replacement.Options, o => o.ObjectIds!.Contains(guardians[1]));
            await game.Act("P1", new PayCostCommand(replacement.Id, "RULE_REPLACEMENT", [chosen.Id]));
            Assert.Contains(host, game.State.PlayerZones["P1"].Base); Assert.Contains(victim, game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(victimGeneration + 1, game.State.CardObjects[victim].ObjectGeneration); Assert.Empty(game.State.PlayerZones["P1"].MainDeck);
            Assert.Equal(new[] { guardians[1], victim }.Order(), game.State.PlayerZones["P1"].Graveyard.Order());
            if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            Assert.Equal(new ObjectBinding(victim, victimGeneration), game.State.StackItems[^1].LastBreathDraw!.Source);
            var beforeDraw = game.State; var handCount = beforeDraw.PlayerZones["P1"].Hand.Count; var drawPrefix = game.Events().Length;
            const string battleScope = "BATTLE_RESPONSE_DECLARATION_CONTEXT:";
            var declaration = Assert.Single(beforeDraw.UntilEndOfTurnEffects, e => e.StartsWith(battleScope, StringComparison.Ordinal));
            Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind is "BATTLE_CLOSED" or "BATTLEFIELD_CONQUERED");
            // This checkpoint replays the accepted replacement and the earlier
            // allocation. A separate restore while the replacement is pending
            // would replay the same full journal without adding a distinct
            // accepted-history boundary.
            await game.ColdCheckpoint();
            await game.Top();
            var drawEvents = game.Events().Skip(drawPrefix).ToArray();
            var burnout = Assert.Single(drawEvents, e => e.Event.Kind == "BURNOUT_APPLIED");
            var draw = Assert.Single(drawEvents, e => e.Event.Kind == "CARD_DRAWN"); Assert.True(burnout.Sequence < draw.Sequence);
            Assert.Equal(handCount + 1, game.State.PlayerZones["P1"].Hand.Count); Assert.Single(game.State.PlayerZones["P1"].MainDeck);
            Assert.Empty(game.State.PlayerZones["P1"].Graveyard); Assert.Equal(beforeDraw.RngCursor + 1, game.State.RngCursor);
            Assert.Equal(holdWindow + 2, game.State.PlayerScores["P2"]);
            if (winOnDraw)
            {
                Assert.Equal(MatchStatuses.Finished, game.State.Status); Assert.Equal("P2", game.State.WinnerPlayerId);
                var win = Assert.Single(drawEvents, e => e.Event.Kind == "MATCH_WON"); Assert.True(draw.Sequence < win.Sequence);
                Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind is "BATTLE_CLOSED" or "BATTLEFIELD_CONQUERED");
                Assert.Contains(declaration, game.State.UntilEndOfTurnEffects);
                Assert.Equal(beforeDraw.CardObjects[source].IsAttacking, game.State.CardObjects[source].IsAttacking);
                Assert.Equal(beforeDraw.CardObjects[field].ControllerId, game.State.CardObjects[field].ControllerId);
                // RejectCoordinatedForgery also cold-recovers this unchanged terminal state.
                await game.RejectCoordinatedForgery("terminal-invented-conquest-point", game.State with {
                    PlayerScores = new Dictionary<string, int>(game.State.PlayerScores) { ["P2"] = 9 } });
                var hash = MatchStateHasher.Hash(game.State);
                foreach (var command in new GameCommand[] { new PassFocusCommand(), new EndTurnCommand(), new SurrenderCommand() })
                {
                    var result = await new CoreRuleEngine().ResolveAsync(game.State, new("after-natural-win-" + command.CmdType, "P2", command.CmdType), command, default);
                    Assert.False(result.Accepted); Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode); Assert.Empty(result.Events);
                    Assert.Equal(hash, MatchStateHasher.Hash(result.State)); directProbes.Add(new { directEngineOnly = true, command, result });
                }
            }
            else
            {
                Assert.Equal(MatchStatuses.InProgress, game.State.Status); Assert.DoesNotContain(drawEvents, e => e.Event.Kind == "MATCH_WON");
                await FinishBattle(game); Assert.Equal(7, game.State.PlayerScores["P2"]); Assert.Equal("P2", game.State.CardObjects[field].ControllerId);
                Assert.Equal(MatchStatuses.InProgress, game.State.Status); Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "BATTLE_CLOSED");
                Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "BATTLEFIELD_CONQUERED"); Assert.False(game.State.CardObjects[source].IsAttacking);
                var history = Assert.Single(game.State.BattlefieldResolutions, h => h.Kind == "CONQUERED" && h.BattlefieldObjectId == field
                    && h.PlayerId == "P2" && h.SourceObjectId == source);
                Assert.Equal(victimGeneration, Assert.Single(history.PublicObjectBindings!, b => b.ObjectId == victim).Generation);
            }
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(new ResolutionResult(true, null, game.State, [],
                ResolutionResult.BuildSnapshots(game.State), ResolutionResult.BuildPrompts(game.State)));
            Assert.Equal(2, game.State.PlayerScores["P1"]);
            if (!winOnDraw) await game.ColdCheckpoint();
        }
        finally
        {
            await game.Save("legal-burnout-battle-cutoff-" + winOnDraw);
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN_CANONICAL_EVIDENCE") is { Length: > 0 } path)
            {
                var folder = Path.Combine(path, "direct-terminal-probes"); Directory.CreateDirectory(folder);
                await File.WriteAllTextAsync(Path.Combine(folder, winOnDraw + ".json"), JsonSerializer.Serialize(new {
                    scope = "Direct engine rejections after a natural score win; separate from accepted session history.", directProbes },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            }
        }
    }
}
