using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Flow = Riftbound.ConformanceTests.LastBreathDrawBoundaryTests;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathBattlefieldDamageBoundaryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task TransferredAreaSkillsKeepTheirBattlefieldAndOldSourceAfterOwnersActualPrivateReturn()
    {
        var initial = Flow.Add(Position(), "CONTROL", "OGN·203/298", "P2", "HAND");
        for (var i = 0; i < 8; i++) initial = Flow.Add(initial, "CONTROL-RUNE-" + i, i < 3 ? "UNL-R05" : "UNL-R06", "P2", "BASE");
        initial = Flow.Add(Flow.Add(Flow.Add(initial, "DRAW", "OGN·048/298", "P1", "HAND"),
            "DRAW-RUNE-A", "UNL-R06", "P1", "BASE"), "DRAW-RUNE-B", "UNL-R06", "P1", "BASE");
        // Reserve one real normal-turn draw before the later empty-deck response.
        var moved = initial.PlayerZones["P1"].MainDeck.Skip(1).Concat(initial.PlayerZones["P1"].Graveyard).ToArray();
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { MainDeck = initial.PlayerZones["P1"].MainDeck.Take(1).ToArray(),
                Graveyard = [], Hand = [..initial.PlayerZones["P1"].Hand, ..moved] } },
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => moved.Contains(p.Key) ? new ObjectLocationState("P1", "HAND") : p.Value) };
        var game = new Driver(initial);
        try
        {
            for (var i = 0; i < 7; i++) await game.Act("P1", new TapRuneCommand("P1-RUNE-" + i));
            await game.Act("P1", new RecycleRuneCommand("P1-RUNE-6"));
            await game.Act("P1", new PlayCardCommand("UNIT", Unit, [], Destination: "BATTLEFIELD:BF"));
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            for (var i = 0; i < 2; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
                Assert.Equal(Unit, game.State.CardObjects["SONG-" + i].AttachedRuleText!.CardNo);
            }
            await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
            await BattleWinnerPhaseBoundaryTests.PassFocus(game); await game.Act("P1", new EndTurnCommand()); await Flow.Tap(game, "P2");
            for (var i = 0; i < 3; i++) await game.Act("P2", new RecycleRuneCommand("CONTROL-RUNE-" + i));
            await game.Act("P2", new PlayCardCommand("CONTROL", "OGN·203/298", ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P2"].Base); Assert.Equal("P2", game.State.CardObjects["UNIT"].ControllerId);
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
            Assert.All(new[] { "SONG-0", "SONG-1" }, id => Assert.Equal("P1", game.State.CardObjects[id].ControllerId));
            await game.Act("P2", new EndTurnCommand()); await game.Act("P1", new EndTurnCommand()); await Flow.Tap(game, "P2");
            await game.Act("P2", new RecycleRuneCommand("P2-RED"));
            await game.Act("P2", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", [], ["UNIT", "ENEMY"]));
            await game.Act("P2", new PlayCardCommand("RAY", "OGN·009/298", ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); Assert.Equal(3, game.State.TriggerQueue.Count);
            Assert.All(game.State.TriggerQueue, item => {
                Assert.Equal("P2", item.ControllerId); Assert.Equal(generation, item.LastBreathBattlefieldDamage!.Source.Generation);
                Assert.Equal("BF", item.LastBreathBattlefieldDamage.BattlefieldObjectId);
            });
            await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await game.Act("P1", new TapRuneCommand("DRAW-RUNE-A")); await game.Act("P1", new TapRuneCommand("DRAW-RUNE-B"));
            await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", [])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > generation + 1);
            Assert.Equal("P1", game.State.CardObjects["UNIT"].ControllerId); var ownerHand = game.State.PlayerZones["P1"].Hand.ToArray();
            Assert.Equal(3, game.State.StackItems.Count);
            foreach (var view in new[] { ResolutionResult.BuildSnapshots(game.State)["P2"], ResolutionResult.BuildSpectatorSnapshot(game.State) })
                foreach (var item in JsonSerializer.SerializeToElement(view, Json).GetProperty("stack").EnumerateArray())
                {
                    Assert.Equal("HIDDEN", item.GetProperty("sourceObjectId").GetString());
                    Assert.False(item.TryGetProperty("lastBreathBattlefieldDamage", out _));
                }
            for (var i = 0; i < 3; i++)
            {
                var prefix = game.Events().Length; await game.Top(); var effects = game.Events().Skip(prefix).Select(e => e.Event).ToArray();
                Assert.Single(effects, e => e.Kind == "TRIGGER_RESOLVED");
                Assert.Equal(i == 0 ? 2 : 0, effects.Count(e => e.Kind == "DAMAGE_APPLIED"));
                foreach (var death in effects.Where(e => e.Kind == "UNIT_DESTROYED"))
                {
                    var cause = Assert.IsType<DestructionAttribution>(death.Payload["destructionAttribution"]);
                    Assert.Equal(new ObjectBinding("UNIT", generation), cause.Source); Assert.Equal(Unit, cause.CardNo);
                    Assert.Equal("P2", cause.ControllerId); Assert.Equal("P2", cause.ResponsiblePlayerId); Assert.False(cause.FromSpell);
                }
                Assert.Equal(ownerHand, game.State.PlayerZones["P1"].Hand); Assert.Equal(2 - i, game.State.StackItems.Count);
                Assert.Equal(0, game.State.CardObjects["OTHER-VICTIM"].Damage); Assert.Equal(0, game.State.CardObjects["BASE-VICTIM"].Damage);
            }
            Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
            Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Flow.Save("kogmaw-transferred-private-source-three-area-skills", initial, game); }
    }

    [Fact]
    public async Task CanonicalAreaSkillAuthenticatesItsOriginalBattlefieldControllerAndBirthTick()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var template = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "poppy-standby").Decklist;
        var fillers = new[] { "OGN·044/298", "OGN·049/298", "OGN·051/298", "OGN·052/298", "OGN·054/298", "OGN·055/298",
            "OGN·056/298", "OGN·059/298", "OGN·061/298", "OGN·065/298", "OGN·070/298", "OGN·082/298" };
        var main = new[] { Unit, Unit, Unit, "OGN·205/298" }.Concat(fillers.SelectMany(no => Enumerable.Repeat(no, 3))).ToArray();
        var ownerDeck = new OfficialDecklist("OGN·259/298", "OGN·205/298", main, Enumerable.Repeat("UNL-R05", 12).ToArray(), template.Battlefields);
        var enemyMain = template.MainDeck.Where(no => no != "OGN·009/298").ToList();
        while (enemyMain.Count > 37) enemyMain.RemoveAt(enemyMain.FindIndex(no => no != template.ChampionCardNo));
        enemyMain.InsertRange(0, ["OGN·009/298", "OGN·009/298", "OGN·009/298"]);
        var enemyDeck = template with { MainDeck = enemyMain, RuneDeck = Enumerable.Repeat("UNL-R01", 12).ToArray() };
        var decks = new Dictionary<string, OfficialDecklist> { ["P1"] = ownerDeck, ["P2"] = enemyDeck };
        foreach (var deck in decks.Values)
        { var legality = OfficialDeckValidator.Validate(deck, catalog); Assert.True(legality.IsValid, string.Join("; ", legality.Errors)); }
        var initial = MatchReplayInitialStateBuilder.FromSeats("CANONICAL-KOGMAW-AREA-BIRTH", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = 424242 };
        var game = new VenEmpowerCanonicalOpeningTests.CanonicalGame(initial);
        try
        {
            foreach (var (player, deck) in decks)
                await game.Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await game.Act("P1", new ReadyCommand()); await game.Act("P2", new ReadyCommand());
            await game.Act(game.State.ActivePlayerId, new MulliganCommand([])); await game.Act(game.State.OpeningSecondActionPlayerId!, new MulliganCommand([]));
            await game.ColdCheckpoint(); await game.WaitFor("P1", 4, Unit); await game.Tap("P1");
            await game.Act("P1", new RecycleRuneCommand(game.Runes("P1").First()));
            var field = game.State.CardObjects.Values.Single(c => c.CardNo == "OGN·278/298" && c.OwnerId == "P1").ObjectId;
            var source = game.Hand("P1", Unit).First(); await game.Act("P1", new PlayCardCommand(source, Unit, [], Destination: "BATTLEFIELD:" + field));
            while (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
            await game.Act("P1", new EndTurnCommand()); await game.WaitFor("P2", 2, "OGN·009/298"); await game.Tap("P2");
            await game.Act("P2", new RecycleRuneCommand(game.Runes("P2").First()));
            await game.Act("P2", new PlayCardCommand(game.Hand("P2", "OGN·009/298").First(), "OGN·009/298", [source])); await game.Top();
            var item = Assert.Single(game.State.StackItems); var birth = Assert.IsType<LastBreathBattlefieldDamageContext>(item.LastBreathBattlefieldDamage);
            Assert.Equal(field, birth.BattlefieldObjectId); await game.ColdCheckpoint();
            var other = game.State.CardObjects.Values.First(c => c.Tags.Contains(CardObjectTags.BattlefieldCard) && c.ObjectId != field).ObjectId;
            await game.RejectCoordinatedForgery("area-skill-original-battlefield", game.State with { StackItems = [item with {
                StackItemId = item.StackItemId.Replace("::BATTLEFIELD::" + field, "::BATTLEFIELD::" + other, StringComparison.Ordinal),
                LastBreathBattlefieldDamage = birth with { BattlefieldObjectId = other } }] });
            await game.RejectCoordinatedForgery("area-skill-death-controller", game.State with { StackItems = [item with {
                ControllerId = "P2", LastBreathBattlefieldDamage = birth with { ControllerId = "P2" } }] });
            await game.RejectCoordinatedForgeryTwiceThenRecover("area-skill-birth-tick", game.State with { StackItems = [item with {
                LastBreathBattlefieldDamage = birth with { CreatedAtTick = birth.CreatedAtTick - 1 } }] });
            await game.Top(); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.ColdCheckpoint();
        }
        finally { await game.Save("last-breath-area-original-battlefield-and-birth-required"); }
    }
}
