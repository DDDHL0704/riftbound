using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Game = Riftbound.ConformanceTests.VenEmpowerCanonicalOpeningTests.CanonicalGame;

namespace Riftbound.ConformanceTests;

public sealed class CanonicalAreaGuardianOpeningTests
{
    [Fact]
    public async Task LegalBlueGreenAndGreenPurpleOpeningsKeepReplacementNestedDrawAndAllThreeAreaSkillsThroughColdRegistryRecovery()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var templates = PreconstructedDeckCatalog.Build(catalog);
        var fields = templates.Single(d => d.Id == "poppy-standby").Decklist.Battlefields;
        var blueGreen = templates.Single(d => d.Id == "lillia-lowcurve").Decklist;
        var wanted = new[] { "OGN·096/298", "SFD·051/221", "OGN·085/298" };
        var additions = wanted.SelectMany(no => Enumerable.Repeat(no, 3)).ToArray();
        var main = blueGreen.MainDeck.Where(no => !wanted.Contains(no)).ToList();
        while (main.Count > 40 - additions.Length) main.RemoveAt(main.FindIndex(no => no != blueGreen.ChampionCardNo));
        main.InsertRange(0, additions);
        var ownerDeck = blueGreen with { MainDeck = main, RuneDeck = Enumerable.Repeat("UNL-R02", 12).ToArray(), Battlefields = fields };
        var greenFillers = new[] { "OGN·044/298", "OGN·049/298", "OGN·051/298", "OGN·052/298", "OGN·054/298",
            "OGN·055/298", "OGN·056/298", "OGN·059/298", "OGN·061/298", "OGN·065/298" };
        var enemyMain = new[] { "OGN·190/298", "SFD·059/221", "OGN·205/298" }.SelectMany(no => Enumerable.Repeat(no, 3))
            .Concat(greenFillers.SelectMany(no => Enumerable.Repeat(no, 3))).Append("OGN·070/298").ToArray();
        var sourceDeck = new OfficialDecklist("OGN·259/298", "OGN·205/298", enemyMain,
            Enumerable.Repeat("UNL-R02", 6).Concat(Enumerable.Repeat("UNL-R05", 6)).ToArray(), fields);
        var decks = new Dictionary<string, OfficialDecklist> { ["P1"] = ownerDeck, ["P2"] = sourceDeck };
        foreach (var deck in decks.Values)
        {
            var valid = OfficialDeckValidator.Validate(deck, catalog); Assert.True(valid.IsValid, string.Join("; ", valid.Errors));
            Assert.Equal(40, deck.MainDeck.Count); Assert.Equal(12, deck.RuneDeck.Count); Assert.Equal(3, deck.Battlefields.Count);
        }
        var initial = MatchReplayInitialStateBuilder.FromSeats("CANONICAL-AREA-GUARDIAN-NESTED-DRAW",
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
                    && game.Hand("P1", "OGN·096/298").Count() >= 2 && game.Hand("P1", "SFD·051/221").Count() >= 2
                    && game.Hand("P1", "OGN·085/298").Any() && game.Hand("P2", "OGN·190/298").Any()
                    && game.Hand("P2", "SFD·059/221").Count() >= 2) break;
                await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            }
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BURNOUT_APPLIED"); await game.ColdCheckpoint();
            Assert.Equal(0, game.State.RunePools["P1"].Mana); await game.Tap("P1"); Assert.Equal(12, game.State.RunePools["P1"].Mana);
            var sentinels = game.Hand("P1", "OGN·096/298").Take(2).ToArray(); var host = sentinels[0]; var victim = sentinels[1];
            foreach (var id in sentinels) await game.Act("P1", new PlayCardCommand(id, "OGN·096/298", []));
            var generation = game.State.CardObjects[host].ObjectGeneration; var victimGeneration = game.State.CardObjects[victim].ObjectGeneration;
            var guardians = game.Hand("P1", "SFD·051/221").Take(2).ToArray();
            foreach (var id in guardians)
            {
                await game.Act("P1", new PlayCardCommand(id, "SFD·051/221", [])); await Recycle(game, "P1", "UNL-R02");
                await game.Act("P1", new AssembleEquipmentCommand(id, host, ["ASSEMBLE_GREEN"])); await game.Top();
            }
            Assert.Equal(4, game.State.RunePools["P1"].Mana); Assert.Equal(3, game.Might("P1", host)); await game.ColdCheckpoint();
            await game.Act("P1", new EndTurnCommand()); Assert.Equal("P2", game.State.TurnPlayerId);
            Assert.Equal(0, game.State.RunePools["P2"].Mana); await game.Tap("P2"); Assert.Equal(12, game.State.RunePools["P2"].Mana);
            var source = game.Hand("P2", "OGN·190/298").First(); await Recycle(game, "P2", "UNL-R05");
            await game.Act("P2", new PlayCardCommand(source, "OGN·190/298", []));
            var sourceGeneration = game.State.CardObjects[source].ObjectGeneration;
            foreach (var song in game.Hand("P2", "SFD·059/221").Take(2).ToArray())
            {
                await Recycle(game, "P2", "UNL-R02"); await game.Act("P2", new PlayCardCommand(song, "SFD·059/221", []));
                await Recycle(game, "P2", "UNL-R02"); await game.Act("P2", new AssembleEquipmentCommand(song, source, ["ASSEMBLE_GREEN"])); await game.Top();
            }
            Assert.Equal(1, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower); await game.ColdCheckpoint();
            await game.Act("P2", new EndTurnCommand());
            Assert.False(game.State.CardObjects[host].IsExhausted); Assert.False(game.State.CardObjects[victim].IsExhausted);
            var field = game.State.CardObjects.Values.Single(c => c.CardNo == "OGN·278/298" && c.OwnerId == "P1").ObjectId;
            await game.Act("P1", new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + field, [], [host, victim]));
            await Focus(game); Assert.Equal("P1", game.State.CardObjects[field].ControllerId); await game.ColdCheckpoint();
            await game.Act("P1", new EndTurnCommand()); Assert.False(game.State.CardObjects[source].IsExhausted);
            await game.Act("P2", new MoveUnitCommand(source, "BASE", "BATTLEFIELD:" + field, []));
            if (game.State.FocusPlayerId != "P1") await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
            Assert.Equal("P1", game.State.FocusPlayerId); Assert.Equal(0, game.State.RunePools["P1"].Mana);
            await game.Tap("P1"); var mana = game.State.RunePools["P1"].Mana;
            await game.Act("P1", new PlayCardCommand(game.Hand("P1", "OGN·085/298").First(), "OGN·085/298", [source]));
            Assert.Equal(mana - 5, game.State.RunePools["P1"].Mana); await game.Top();
            Assert.Contains(source, game.State.PlayerZones["P2"].Graveyard); Assert.Equal(3, game.State.TriggerQueue.Count);
            Assert.Equal(3, game.State.TriggerQueue.Select(t => Assert.IsType<LastBreathBattlefieldDamageContext>(t.LastBreathBattlefieldDamage).InstanceId).Distinct().Count());
            await game.ColdCheckpoint();
            await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            var top = game.State.StackItems[^1]; var birth = Assert.IsType<LastBreathBattlefieldDamageContext>(top.LastBreathBattlefieldDamage);
            await game.RejectCoordinatedForgery("canonical-guardian-area-birth-tick", game.State with { StackItems = game.State.StackItems
                .Select(item => item == top ? item with { LastBreathBattlefieldDamage = birth with { CreatedAtTick = birth.CreatedAtTick - 1 } } : item).ToArray() });
            var hand = game.State.PlayerZones["P1"].Hand.ToArray(); var deckAtDeath = game.State.PlayerZones["P1"].MainDeck.ToArray();
            var runesAtDeath = game.State.PlayerZones["P1"].RuneDeck.ToArray(); Assert.NotEmpty(deckAtDeath); var prefix = game.Events().Length;
            await game.Top(); var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
            Assert.Equal("P1", request.PlayerId); Assert.Equal(2, request.Options.Count); await game.ColdCheckpoint();
            var selected = guardians[1]; var choice = Assert.Single(request.Options, o => o.ObjectIds!.Contains(selected));
            await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [choice.Id])); await game.ColdCheckpoint();
            var effects = game.Events().Skip(prefix).ToArray();
            var consumed = Assert.Single(effects, e => e.Event.Kind == "EQUIPMENT_DESTROYED" && Target(e.Event) == selected);
            var recalled = Assert.Single(effects, e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == host);
            var dead = Assert.Single(effects, e => e.Event.Kind == "UNIT_DESTROYED" && Target(e.Event) == victim);
            Assert.True(consumed.Sequence < recalled.Sequence && recalled.Sequence < dead.Sequence);
            Assert.Equal(2, effects.Count(e => e.Event.Kind == "DAMAGE_APPLIED"));
            Assert.All(effects.Where(e => e.Event.Kind == "DAMAGE_APPLIED"), e => Assert.Equal(4, Convert.ToInt32(e.Event.Payload["damage"])));
            Assert.DoesNotContain(effects, e => e.Event.Kind == "CARD_DRAWN" || e.Event.Kind == "UNIT_DESTROYED" && Target(e.Event) == host);
            Assert.Equal(generation, game.State.CardObjects[host].ObjectGeneration); Assert.True(game.State.CardObjects[host].IsExhausted);
            Assert.Equal(0, game.State.CardObjects[host].Damage); Assert.Contains(host, game.State.PlayerZones["P1"].Base);
            Assert.Equal(host, game.State.CardObjects[guardians[0]].AttachedToObjectId); Assert.Contains(selected, game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(hand, game.State.PlayerZones["P1"].Hand); Assert.Equal(deckAtDeath, game.State.PlayerZones["P1"].MainDeck);
            var cause = Assert.IsType<DestructionAttribution>(dead.Event.Payload["destructionAttribution"]);
            Assert.Equal(new ObjectBinding(source, sourceGeneration), cause.Source); Assert.Equal("OGN·190/298", cause.CardNo);
            Assert.Equal("P2", cause.ControllerId); Assert.Equal("P2", cause.ResponsiblePlayerId); Assert.False(cause.FromSpell);
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: [Assert.Single(game.State.TriggerQueue).TriggerId]));
            var draw = Assert.IsType<LastBreathDrawContext>(game.State.StackItems[^1].LastBreathDraw);
            Assert.Equal(new ObjectBinding(victim, victimGeneration), draw.Source); Assert.Equal("P1", draw.ControllerId);
            Assert.Equal(3, game.State.StackItems.Count); await game.Top(); await game.ColdCheckpoint();
            Assert.Equal(hand.Concat(deckAtDeath.Take(1)), game.State.PlayerZones["P1"].Hand); Assert.Equal(deckAtDeath.Skip(1), game.State.PlayerZones["P1"].MainDeck);
            for (var i = 0; i < 2; i++)
            {
                Assert.NotNull(game.State.StackItems[^1].LastBreathBattlefieldDamage); var before = game.Events().Length;
                await game.Top(); Assert.DoesNotContain(game.Events().Skip(before), e => e.Event.Kind is "DAMAGE_APPLIED" or "CARD_DRAWN");
                Assert.Equal(0, game.State.CardObjects[host].Damage); await game.ColdCheckpoint();
            }
            Assert.Equal(runesAtDeath, game.State.PlayerZones["P1"].RuneDeck); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
            var scores = game.State.PlayerScores.ToDictionary(); var closing = game.Events().Length; await Focus(game);
            Assert.Single(game.Events(), e => e.Event.Kind == "BATTLE_NO_RESULT"); Assert.Single(game.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
            Assert.False(game.State.BattleState.IsActive); Assert.False(game.State.SpellDuelState.IsActive); Assert.Equal(scores, game.State.PlayerScores);
            Assert.DoesNotContain(game.Events().Skip(closing), e => e.Event.Kind is "SCORE_GAINED" or "BATTLEFIELD_CONQUERED");
            foreach (var player in new[] { "P1", "P2" })
                Assert.Equal(12, game.State.CardObjects.Values.Count(c => c.OwnerId == player && c.Tags.Contains(CardObjectTags.RuneCard)));
            await game.ColdCheckpoint();
        }
        finally { await game.Save("legal-bluegreen-greenpurple-guardian-area-nested-draw"); }
    }

    private static async Task Recycle(Game game, string player, string no)
        => await game.Act(player, new RecycleRuneCommand(game.Runes(player).First(id => game.State.CardObjects[id].CardNo == no)));
    private static async Task Focus(Game game)
    {
        for (var step = 0; game.State.FocusPlayerId is { } player; step++)
        { Assert.InRange(step, 0, 3); await game.Act(player, new PassFocusCommand()); }
    }
    private static string? Target(GameEvent value) => value.Payload.GetValueOrDefault("targetObjectId") as string;
}
