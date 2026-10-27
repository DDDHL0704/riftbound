using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Game = Riftbound.ConformanceTests.VenEmpowerCanonicalOpeningTests.CanonicalGame;

namespace Riftbound.ConformanceTests;

public sealed class CanonicalTerminalDamageChoiceOpeningTests
{
    [Theory]
    [InlineData("P1")] [InlineData("P2")] [InlineData("CONTINUE")]
    public async Task ARealPaidEnemyChoiceAndLowerInverseFreezeAtEitherConcessionAndResumeOnlyInTheLiveControl(string ending)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var fields = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "poppy-standby").Decklist.Battlefields;
        var ownerCards = Copies("VEN·035", "UNL-156/219", "VEN·130", "SFD·051/221", "OGN·238/298")
            .Concat(Copies("OGN·044/298", "OGN·049/298", "OGN·051/298", "OGN·052/298", "OGN·054/298",
                "OGN·055/298", "OGN·056/298", "OGN·059/298", "OGN·061/298")).Take(40).ToArray();
        var enemyCards = Copies("OGN·033/298", "OGN·030/298", "OGN·190/298")
            .Concat(Copies("OGN·012/298", "OGN·013/298", "OGN·024/298", "OGN·026/298", "OGN·027/298",
                "OGN·029/298", "OGN·039/298", "OGN·171/298", "OGN·180/298", "OGN·183/298", "OGN·185/298")).Take(40).ToArray();
        var decks = new Dictionary<string, OfficialDecklist> {
            ["P1"] = new("OGN·261/298", "OGN·238/298", ownerCards,
                Enumerable.Repeat("UNL-R02", 6).Concat(Enumerable.Repeat("UNL-R06", 6)).ToArray(), fields),
            ["P2"] = new("OGN·251/298", "OGN·030/298", enemyCards,
                Enumerable.Repeat("UNL-R01", 6).Concat(Enumerable.Repeat("UNL-R05", 6)).ToArray(), fields) };
        foreach (var deck in decks.Values)
        {
            var valid = OfficialDeckValidator.Validate(deck, catalog); Assert.True(valid.IsValid, string.Join("; ", valid.Errors));
            Assert.Equal(40, deck.MainDeck.Count); Assert.Equal(12, deck.RuneDeck.Count); Assert.Equal(3, deck.Battlefields.Count);
        }
        var initial = MatchReplayInitialStateBuilder.FromSeats("CANONICAL-TERMINAL-DAMAGE-CHOICE",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = 424242 };
        var game = new Game(initial); var probes = new List<object>();
        try
        {
            foreach (var (player, deck) in decks)
                await game.Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await game.Act("P1", new ReadyCommand()); await game.Act("P2", new ReadyCommand());
            await game.Act(game.State.ActivePlayerId, new MulliganCommand([])); await game.Act(game.State.OpeningSecondActionPlayerId!, new MulliganCommand([]));
            await game.ColdCheckpoint();
            for (var step = 0; ; step++)
            {
                await FinishBeginning(game); Assert.InRange(step, 0, 59); Assert.Equal(MatchStatuses.InProgress, game.State.Status);
                if (game.State.TurnPlayerId == "P1" && game.Runes("P1").Length >= 6 && game.Runes("P2").Length >= 2
                    && game.Runes("P1").Any(id => game.State.CardObjects[id].CardNo == "UNL-R02")
                    && game.Runes("P2").Any(id => game.State.CardObjects[id].CardNo == "UNL-R01")
                    && game.Hand("P1", "VEN·035").Any() && game.Hand("P1", "UNL-156/219").Any()
                    && game.Hand("P2", "OGN·033/298").Any()) break;
                await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            }
            foreach (var id in game.Runes("P1").Take(6).ToArray()) await game.Act("P1", new TapRuneCommand(id));
            var unit = game.Hand("P1", "UNL-156/219").First(); await game.Act("P1", new PlayCardCommand(unit, "UNL-156/219", []));
            var generation = game.State.CardObjects[unit].ObjectGeneration; var creatingSpell = game.Hand("P1", "VEN·035").First();
            await Recycle(game, "P1", "UNL-R02");
            await game.Act("P1", new PlayCardCommand(creatingSpell, "VEN·035", [unit], Mode: "EMPOWER_THEN_REMOVE")); await game.Top();
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.True(game.State.CardObjects[unit].IsEmpowered); Assert.Contains(creatingSpell, game.State.PlayerZones["P1"].Graveyard);
            await game.ColdCheckpoint(); await game.Act("P1", new EndTurnCommand());
            var inverse = Assert.Single(game.State.StackItems); Assert.NotNull(inverse.DelayedEmpower); Assert.Equal(unit, inverse.DelayedEmpower!.Unit.ObjectId);
            await game.Act("P1", new PassPriorityCommand()); Assert.Equal("P2", game.State.PriorityPlayerId);
            foreach (var id in game.Runes("P2").Take(2).ToArray()) await game.Act("P2", new TapRuneCommand(id));
            await Recycle(game, "P2", "UNL-R01"); var robbery = game.Hand("P2", "OGN·033/298").First();
            await game.Act("P2", new PlayCardCommand(robbery, "OGN·033/298", [unit])); await game.Top();
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("P1", choice.PlayerId); Assert.Equal("TARGET_CONTROLLER_DRAW_REPLACEMENT", choice.ChoiceWindow);
            Assert.Equal(2, game.State.StackItems.Count); Assert.Equal(inverse.StackItemId, game.State.StackItems[0].StackItemId);
            Assert.Equal(0, game.State.CardObjects[unit].Damage); Assert.True(game.State.CardObjects[unit].IsEmpowered);
            Assert.Equal(0, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            var pools = JsonSerializer.Serialize(game.State.RunePools); var runes = game.State.PlayerZones["P2"].RuneDeck.ToArray();
            var scores = game.State.PlayerScores.ToDictionary(); var hand = game.State.PlayerZones["P2"].Hand.ToArray();
            var deckAtChoice = game.State.PlayerZones["P2"].MainDeck.ToArray(); var before = game.Events().Length;
            await game.ColdCheckpoint();
            if (ending == "CONTINUE")
            {
                await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
                Assert.Contains(unit, game.State.PlayerZones["P1"].Graveyard); Assert.Equal(generation + 1, game.State.CardObjects[unit].ObjectGeneration);
                var death = Assert.Single(game.Events().Skip(before), e => e.Event.Kind == "UNIT_DESTROYED");
                var cause = Assert.IsType<DestructionAttribution>(death.Event.Payload["destructionAttribution"]);
                Assert.True(cause.FromSpell); Assert.Equal("P2", cause.ResponsiblePlayerId); Assert.Equal(robbery, cause.Source.ObjectId);
                Assert.Single(game.State.StackItems); Assert.Equal(generation, game.State.StackItems[0].DelayedEmpower!.Unit.Generation);
                await game.ColdCheckpoint(); await game.Top(); await FinishBeginning(game);
                Assert.Equal(MatchStatuses.InProgress, game.State.Status); Assert.Equal("P2", game.State.TurnPlayerId);
                Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.EndStep);
                Assert.DoesNotContain(game.Events().Skip(before), e => e.Event.Kind == "MATCH_WON");
            }
            else
            {
                var frozen = game.State; await game.Act(ending, new SurrenderCommand());
                Assert.Equal(MatchStatuses.Finished, game.State.Status); Assert.Equal(ending == "P1" ? "P2" : "P1", game.State.WinnerPlayerId);
                Assert.Equal(frozen.TurnNumber, game.State.TurnNumber); Assert.Equal(frozen.TurnPlayerId, game.State.TurnPlayerId);
                Assert.Equal(frozen.EndStep, game.State.EndStep); Assert.Equal(choice, game.State.PendingCardChoice);
                Assert.Equal(frozen.StackItems, game.State.StackItems); Assert.Equal(pools, JsonSerializer.Serialize(game.State.RunePools));
                Assert.Equal(runes, game.State.PlayerZones["P2"].RuneDeck); Assert.Equal(hand, game.State.PlayerZones["P2"].Hand);
                Assert.Equal(deckAtChoice, game.State.PlayerZones["P2"].MainDeck); Assert.Equal(scores, game.State.PlayerScores);
                Assert.Equal(generation, game.State.CardObjects[unit].ObjectGeneration); Assert.True(game.State.CardObjects[unit].IsEmpowered);
                Assert.Equal(0, game.State.CardObjects[unit].Damage);
                Assert.Equal(new[] { "MATCH_WON" }, game.Events().Skip(before).Select(e => e.Event.Kind));
                await game.ColdCheckpoint();
                await game.RejectCoordinatedForgery("terminal-pretended-lower-inverse-ran", game.State with {
                    CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                        [unit] = game.State.CardObjects[unit] with { IsEmpowered = false } } });
                var hash = MatchStateHasher.Hash(game.State);
                foreach (var (type, command) in new (string, GameCommand)[] {
                    (CommandTypes.ChooseCards, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])),
                    (CommandTypes.PassPriority, new PassPriorityCommand()), (CommandTypes.EndTurn, new EndTurnCommand()),
                    (CommandTypes.Surrender, new SurrenderCommand()) })
                {
                    var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("finished-probe-" + type, "P1", type), command, default);
                    Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode); Assert.Empty(rejected.Events);
                    Assert.Equal(hash, MatchStateHasher.Hash(rejected.State)); probes.Add(new { type, directEngineOnly = true, result = rejected });
                }
            }
            await game.ColdCheckpoint();
        }
        finally
        {
            await game.Save("legal-terminal-choice-" + ending);
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN_CANONICAL_EVIDENCE") is { Length: > 0 } path)
            {
                var folder = Path.Combine(path, "terminal-probes"); Directory.CreateDirectory(folder);
                await File.WriteAllTextAsync(Path.Combine(folder, ending + ".json"), JsonSerializer.Serialize(new {
                    scope = "Post-finish probes are direct CoreRuleEngine resolutions, separate from the accepted session history.", probes },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            }
        }
    }

    private static IEnumerable<string> Copies(params string[] cards) => cards.SelectMany(no => Enumerable.Repeat(no, 3));
    private static async Task Recycle(Game game, string player, string no)
        => await game.Act(player, new RecycleRuneCommand(game.Runes(player).First(id => game.State.CardObjects[id].CardNo == no)));
    private static async Task FinishBeginning(Game game)
    {
        for (var i = 0; game.State.Phase == MatchPhases.TurnStart; i++)
        {
            Assert.InRange(i, 0, 7);
            if (game.State.TriggerQueue.Count > 0)
                await game.Act(game.State.TriggerQueue[0].ControllerId, new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else if (game.State.StackItems.Count > 0) await game.Top();
            else Assert.Fail("The actual beginning phase has no ordinary next skill resolution.");
        }
    }
}
