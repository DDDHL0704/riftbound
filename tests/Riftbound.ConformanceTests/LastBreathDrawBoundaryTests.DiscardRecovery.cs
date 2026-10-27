using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathDrawBoundaryTests
{
    [Fact]
    public async Task TransferredAgentChoicesKeepTheirControllerAfterSourceReturnsToOwnersPrivateHand()
    {
        var (initial, game, generation) = await CaptureDraws(privateDraw: true, transferControl: true, sourceCardNo: "OGN·178/298");
        try
        {
            Assert.Equal(3, game.State.TriggerQueue.Count);
            Assert.All(game.State.TriggerQueue, t => Assert.Equal("P2", t.LastBreathDraw!.ControllerId));
            await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await game.Act("P1", new TapRuneCommand("DRAW-RUNE-A")); await game.Act("P1", new TapRuneCommand("DRAW-RUNE-B"));
            await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", [])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
            Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > generation + 1);
            Assert.Equal(3, game.State.StackItems.Count); var ownerHand = game.State.PlayerZones["P1"].Hand.ToArray();
            var expectedDiscards = new[] { 1, 2, 2 };
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal("P2", game.State.StackItems[^1].ControllerId); var prefix = game.Events().Length;
                var hand = game.State.PlayerZones["P2"].Hand.ToArray(); Assert.Equal(expectedDiscards[i], hand.Length);
                await game.Top();
                if (i == 0) Assert.Null(game.State.PendingHandChoice);
                else
                {
                    var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
                    Assert.Equal("P2", choice.PlayerId); Assert.Equal(generation, choice.LastBreathDraw!.Source.Generation);
                    foreach (var viewer in new[] { "P2", "" })
                    {
                        var snapshot = viewer.Length == 0 ? ResolutionResult.BuildSpectatorSnapshot(game.State) : ResolutionResult.BuildSnapshots(game.State)[viewer];
                        var payload = JsonSerializer.SerializeToElement(snapshot.Timing["pendingHandChoice"], Json);
                        Assert.Equal("HIDDEN", payload.GetProperty("sourceObjectId").GetString());
                        Assert.False(payload.TryGetProperty("lastBreathDraw", out _));
                    }
                    foreach (var metadata in new[] { game.PromptFor("P2").View!.Metadata,
                        Assert.Single(game.PromptFor("P2").Candidates!, c => c.Action == CommandTypes.ChooseHandCards).Metadata })
                        Assert.Equal("HIDDEN", Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(metadata)["sourceObjectId"]);
                    AssertAgentChoiceBindings(game);
                    await game.Act("P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, hand));
                }
                Assert.Null(game.State.PendingHandChoice); Assert.Equal(2, game.State.PlayerZones["P2"].Hand.Count);
                var effects = game.Events().Skip(prefix).Select(e => e.Event).ToArray();
                Assert.Equal(expectedDiscards[i], effects.Count(e => e.Kind == "CARD_DISCARDED"));
                Assert.All(effects.Where(e => e.Kind is "CARD_DISCARDED" or "CARD_DRAWN"), e => Assert.Equal("P2", e.Payload["playerId"]));
                Assert.Equal(ownerHand, game.State.PlayerZones["P1"].Hand); Assert.Equal(2 - i, game.State.StackItems.Count);
            }
            Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Save("agent-transferred-private-source-three-choices", initial, game); }
    }

    [Fact]
    public async Task CanonicalAgentChoiceStillAuthenticatesItsOriginalControllerAndBirthTick()
    {
        const string agent = "OGN·178/298"; var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var template = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "poppy-standby").Decklist;
        var fillers = new[] { "OGN·044/298", "OGN·049/298", "OGN·051/298", "OGN·052/298", "OGN·054/298", "OGN·055/298",
            "OGN·056/298", "OGN·059/298", "OGN·061/298", "OGN·065/298", "OGN·070/298", "OGN·082/298" };
        var main = new[] { agent, agent, agent, "OGN·205/298" }.Concat(fillers.SelectMany(no => Enumerable.Repeat(no, 3))).ToArray();
        var ownerDeck = new OfficialDecklist("OGN·259/298", "OGN·205/298", main, Enumerable.Repeat("UNL-R05", 12).ToArray(), template.Battlefields);
        var enemyMain = template.MainDeck.Where(no => no != Kill).ToList();
        while (enemyMain.Count > 37) enemyMain.RemoveAt(enemyMain.FindIndex(no => no != template.ChampionCardNo));
        enemyMain.InsertRange(0, [Kill, Kill, Kill]);
        var enemyDeck = template with { MainDeck = enemyMain, RuneDeck = Enumerable.Repeat("UNL-R06", 12).ToArray() };
        var decks = new Dictionary<string, OfficialDecklist> { ["P1"] = ownerDeck, ["P2"] = enemyDeck };
        foreach (var deck in decks.Values)
        { var legality = OfficialDeckValidator.Validate(deck, catalog); Assert.True(legality.IsValid, string.Join("; ", legality.Errors)); }
        var initial = MatchReplayInitialStateBuilder.FromSeats("CANONICAL-AGENT-CHOICE", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = 424242 };
        var game = new VenEmpowerCanonicalOpeningTests.CanonicalGame(initial);
        try
        {
            foreach (var (player, deck) in decks)
                await game.Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await game.Act("P1", new ReadyCommand()); await game.Act("P2", new ReadyCommand());
            await game.Act(game.State.ActivePlayerId, new MulliganCommand([])); await game.Act(game.State.OpeningSecondActionPlayerId!, new MulliganCommand([]));
            await game.ColdCheckpoint(); await game.WaitFor("P1", 6, agent); await game.Tap("P1");
            await game.Act("P1", new RecycleRuneCommand(game.Runes("P1").First()));
            var source = game.Hand("P1", agent).First(); await game.Act("P1", new PlayCardCommand(source, agent, []));
            await game.Act("P1", new EndTurnCommand()); await game.WaitFor("P2", 4, Kill); await game.Tap("P2");
            for (var i = 0; i < 2; i++) await game.Act("P2", new RecycleRuneCommand(game.Runes("P2").First()));
            await game.Act("P2", new PlayCardCommand(game.Hand("P2", Kill).First(), Kill, [source])); await game.Top(); await game.Top();
            var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
            var birth = Assert.IsType<LastBreathDrawContext>(choice.LastBreathDraw); await game.ColdCheckpoint();
            Assert.True(game.State.PlayerZones["P2"].Hand.Count >= 2);
            await game.RejectCoordinatedForgery("agent-choice-death-controller", game.State with { PendingHandChoice = choice with {
                PlayerId = "P2", LegalObjectIds = game.State.PlayerZones["P2"].Hand.ToArray(), LastBreathDraw = birth with { ControllerId = "P2" } } });
            await game.RejectCoordinatedForgeryTwiceThenRecover("agent-choice-birth-tick", game.State with { PendingHandChoice = choice with {
                LastBreathDraw = birth with { CreatedAtTick = birth.CreatedAtTick - 1 } } });
            var hand = game.State.PlayerZones["P1"].Hand.ToArray(); var deckBefore = game.State.PlayerZones["P1"].MainDeck.ToArray();
            await game.Act("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, hand.Take(2).ToArray()));
            Assert.Null(game.State.PendingHandChoice);
            Assert.Equal(hand.Skip(2).Concat(deckBefore.Take(2)), game.State.PlayerZones["P1"].Hand); await game.ColdCheckpoint();
        }
        finally { await game.Save("last-breath-agent-original-choice-birth-required"); }
    }
}
