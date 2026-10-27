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
    public async Task PaidVolibearAttackEitherDistributesThroughGuardianAndDrawOrFollowsItsRealRecallBeforeResolution(bool withdraw)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var templates = PreconstructedDeckCatalog.Build(catalog);
        var fields = templates.Single(d => d.Id == "poppy-standby").Decklist.Battlefields;
        var owner = templates.Single(d => d.Id == "lillia-lowcurve").Decklist;
        var wanted = new[] { "OGN·096/298", "SFD·051/221" };
        var additions = Copies(wanted).ToArray(); var main = owner.MainDeck.Where(no => !wanted.Contains(no)).ToList();
        while (main.Count > 40 - additions.Length) main.RemoveAt(main.FindIndex(no => no != owner.ChampionCardNo));
        main.InsertRange(0, additions);
        var defenderDeck = owner with { MainDeck = main, RuneDeck = Enumerable.Repeat("UNL-R02", 12).ToArray(), Battlefields = fields };
        var attackerMain = Copies("OGN·041/298", "OGS·011/024", "OGN·030/298")
            .Concat(Copies("OGN·012/298", "OGN·013/298", "OGN·024/298", "OGN·026/298", "OGN·027/298",
                "OGN·029/298", "OGN·039/298", "OGN·171/298", "OGN·180/298", "OGN·183/298", "OGN·185/298")).Take(40).ToArray();
        var attackerDeck = new OfficialDecklist("OGN·251/298", "OGN·030/298", attackerMain,
            Enumerable.Repeat("UNL-R01", 6).Concat(Enumerable.Repeat("UNL-R05", 6)).ToArray(), fields);
        var decks = new Dictionary<string, OfficialDecklist> { ["P1"] = defenderDeck, ["P2"] = attackerDeck };
        foreach (var deck in decks.Values)
        {
            var valid = OfficialDeckValidator.Validate(deck, catalog); Assert.True(valid.IsValid, string.Join("; ", valid.Errors));
            Assert.Equal(40, deck.MainDeck.Count); Assert.Equal(12, deck.RuneDeck.Count); Assert.Equal(3, deck.Battlefields.Count);
        }
        var initial = MatchReplayInitialStateBuilder.FromSeats("CANONICAL-DISTRIBUTED-GUARDIAN",
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
                await Beginning(game); Assert.InRange(step, 0, 79); Assert.Equal(MatchStatuses.InProgress, game.State.Status);
                if (game.State.TurnPlayerId == "P1" && game.Runes("P1").Length == 12 && game.Runes("P2").Length == 12
                    && game.Hand("P1", "OGN·096/298").Count() >= 2 && game.Hand("P1", "SFD·051/221").Count() >= 2
                    && game.Hand("P2", "OGN·041/298").Any() && game.Hand("P2", "OGS·011/024").Any()) break;
                await game.Act(game.State.TurnPlayerId!, new EndTurnCommand());
            }
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BURNOUT_APPLIED");
            foreach (var id in game.Runes("P1").Take(8).ToArray()) await game.Act("P1", new TapRuneCommand(id));
            var sentinels = game.Hand("P1", "OGN·096/298").Take(2).ToArray(); var host = sentinels[0]; var victim = sentinels[1];
            foreach (var id in sentinels) await game.Act("P1", new PlayCardCommand(id, "OGN·096/298", []));
            var hostGeneration = game.State.CardObjects[host].ObjectGeneration; var victimGeneration = game.State.CardObjects[victim].ObjectGeneration;
            var guardians = game.Hand("P1", "SFD·051/221").Take(2).ToArray();
            foreach (var id in guardians)
            {
                await game.Act("P1", new PlayCardCommand(id, "SFD·051/221", [])); await Recycle(game, "P1", "UNL-R02");
                await game.Act("P1", new AssembleEquipmentCommand(id, host, ["ASSEMBLE_GREEN"])); await game.Top();
            }
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            Assert.Equal(3, game.Might("P1", host)); await game.ColdCheckpoint();
            await game.Act("P1", new EndTurnCommand()); await Beginning(game); await game.Tap("P2");
            await Recycle(game, "P2", "UNL-R01"); await Recycle(game, "P2", "UNL-R01");
            var source = game.Hand("P2", "OGN·041/298").First();
            await game.Act("P2", new PlayCardCommand(source, "OGN·041/298", []));
            var sourceGeneration = game.State.CardObjects[source].ObjectGeneration;
            Assert.Equal(2, game.State.RunePools["P2"].Mana); Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            Assert.Equal(9, game.Might("P2", source)); await game.ColdCheckpoint();
            await game.Act("P2", new EndTurnCommand()); await Beginning(game);
            var field = game.State.CardObjects.Values.Single(c => c.OwnerId == "P1" && c.CardNo == "OGN·278/298").ObjectId;
            await game.Act("P1", new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + field, [], [host, victim]));
            await Focus(game); Assert.Equal("P1", game.State.CardObjects[field].ControllerId); Assert.Equal(1, game.State.PlayerScores["P1"]);
            await game.Act("P1", new EndTurnCommand()); await Beginning(game); Assert.False(game.State.CardObjects[source].IsExhausted);
            await game.Act("P2", new MoveUnitCommand(source, "BASE", "BATTLEFIELD:" + field, []));
            for (var i = 0; game.State.PendingCardChoice is null; i++)
            {
                Assert.InRange(i, 0, 7);
                if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
                else await game.Act("P2", new DeclareBattleCommand(field, [source], sentinels, ["COMBAT_ASSIGNMENT"]));
            }
            var confirmation = game.State.PendingCardChoice!; Assert.Equal("TRIGGER_CONFIRMATION", confirmation.ChoiceWindow);
            Assert.Equal("P2", confirmation.PlayerId); Assert.Equal(sentinels.Order(), confirmation.LegalObjectIds.Order()); await game.ColdCheckpoint();
            await game.Act("P2", new ChooseCardsCommand(confirmation.ChoiceId, confirmation.ChoiceWindow, sentinels));
            var captured = Assert.Single(game.State.StackItems).DamageDistribution!;
            Assert.Equal(5, captured.InitialDamage); Assert.Equal(new ObjectBinding(source, sourceGeneration), captured.Source);
            Assert.Equal(field, captured.SourceLocation.BattlefieldObjectId); await game.ColdCheckpoint();
            var prefix = game.Events().Length; var hand = game.State.PlayerZones["P1"].Hand.ToArray(); var deckAtAttack = game.State.PlayerZones["P1"].MainDeck.ToArray();
            if (withdraw)
            {
                foreach (var id in game.Runes("P2").Take(2).ToArray()) await game.Act("P2", new TapRuneCommand(id));
                await game.Act("P2", new PlayCardCommand(game.Hand("P2", "OGS·011/024").First(), "OGS·011/024", [source])); await game.Top();
                Assert.Equal(0, game.State.RunePools["P2"].Mana); Assert.Equal("BASE", game.State.ObjectLocations[source].Zone);
                Assert.Equal(sourceGeneration, game.State.CardObjects[source].ObjectGeneration);
                Assert.Equal("BASE", Assert.Single(game.State.StackItems).DamageDistribution!.SourceLocation.Zone); await game.ColdCheckpoint();
                await game.Top(); Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
                Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind is "DAMAGE_APPLIED" or "UNIT_DESTROYED" or "EQUIPMENT_DESTROYED" or "CARD_DRAWN");
                Assert.Equal(hand, game.State.PlayerZones["P1"].Hand); Assert.Equal(deckAtAttack, game.State.PlayerZones["P1"].MainDeck);
                Assert.All(guardians, id => Assert.Equal(host, game.State.CardObjects[id].AttachedToObjectId));
            }
            else
            {
                await game.Top(); var allocation = game.State.PendingCardChoice!;
                Assert.Equal("EFFECT_DAMAGE_DISTRIBUTION", allocation.ChoiceWindow); Assert.Equal("P2", allocation.PlayerId);
                Assert.Equal(5, Assert.Single(game.State.StackItems).DamageDistribution!.ResolutionDamage); Assert.Equal(9, game.Might("P2", source));
                await game.ColdCheckpoint();
                await game.RejectCoordinatedForgery("allocation-invented-empower", game.State with {
                    CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
                        [host] = game.State.CardObjects[host] with { IsEmpowered = true } } });
                await game.Act("P2", new AssignEffectDamageCommand(allocation.ResolvingStackItemId!, [
                    new CombatDamageAssignmentDto(source, host, 3), new CombatDamageAssignmentDto(source, victim, 2)]));
                var replacement = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
                Assert.Equal("P1", replacement.PlayerId); Assert.Equal(2, replacement.Options.Count); await game.ColdCheckpoint();
                var selected = Assert.Single(replacement.Options, o => o.ObjectIds!.Contains(guardians[1]));
                await game.Act("P1", new PayCostCommand(replacement.Id, "RULE_REPLACEMENT", [selected.Id])); await game.ColdCheckpoint();
                var events = game.Events().Skip(prefix).ToArray();
                var consumed = Assert.Single(events, e => e.Event.Kind == "EQUIPMENT_DESTROYED");
                var recall = Assert.Single(events, e => e.Event.Kind == "UNIT_RECALLED_TO_BASE");
                var death = Assert.Single(events, e => e.Event.Kind == "UNIT_DESTROYED");
                Assert.True(consumed.Sequence < recall.Sequence && recall.Sequence < death.Sequence);
                Assert.Equal(2, events.Count(e => e.Event.Kind == "DAMAGE_APPLIED"));
                Assert.Equal(new[] { 2, 3 }, events.Where(e => e.Event.Kind == "DAMAGE_APPLIED").Select(e => Convert.ToInt32(e.Event.Payload["damage"])).Order());
                Assert.Contains(guardians[1], game.State.PlayerZones["P1"].Graveyard); Assert.Equal(host, game.State.CardObjects[guardians[0]].AttachedToObjectId);
                Assert.Equal(hostGeneration, game.State.CardObjects[host].ObjectGeneration); Assert.Contains(host, game.State.PlayerZones["P1"].Base);
                Assert.True(game.State.CardObjects[host].IsExhausted); Assert.Equal(0, game.State.CardObjects[host].Damage);
                Assert.Contains(victim, game.State.PlayerZones["P1"].Graveyard); Assert.Equal(victimGeneration + 1, game.State.CardObjects[victim].ObjectGeneration);
                var cause = Assert.IsType<DestructionAttribution>(death.Event.Payload["destructionAttribution"]);
                Assert.False(cause.FromSpell); Assert.Equal("P2", cause.ResponsiblePlayerId); Assert.Equal(new ObjectBinding(source, sourceGeneration), cause.Source);
                Assert.Equal(hand, game.State.PlayerZones["P1"].Hand); Assert.Equal(deckAtAttack, game.State.PlayerZones["P1"].MainDeck);
                Assert.DoesNotContain(events, e => e.Event.Kind is "CARD_DRAWN" or "BATTLE_CLOSED" or "BATTLEFIELD_CONQUERED");
                if (game.State.TriggerQueue.Count > 0) await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
                Assert.Equal(new ObjectBinding(victim, victimGeneration), game.State.StackItems[^1].LastBreathDraw!.Source);
                await game.ColdCheckpoint(); await game.Top(); Assert.Equal(hand.Length + 1, game.State.PlayerZones["P1"].Hand.Count);
            }
            await FinishBattle(game);
            Assert.Equal(MatchStatuses.InProgress, game.State.Status); Assert.False(game.State.BattleState.IsActive); Assert.Null(game.State.PendingBattleResult);
            Assert.Equal(withdraw ? "P1" : "P2", game.State.CardObjects[field].ControllerId);
            Assert.Equal(1, game.State.PlayerScores["P1"]); Assert.Equal(withdraw ? 0 : 1, game.State.PlayerScores["P2"]);
            Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "BATTLE_CLOSED");
            Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind is "MATCH_WON" or "BURNOUT_APPLIED"); await game.ColdCheckpoint();
        }
        finally { await game.Save("legal-distributed-guardian-" + withdraw); }
    }

    private static IEnumerable<string> Copies(params string[] cards) => cards.SelectMany(no => Enumerable.Repeat(no, 3));
    private static async Task Recycle(Game game, string player, string no)
        => await game.Act(player, new RecycleRuneCommand(game.Runes(player).First(id => game.State.CardObjects[id].CardNo == no)));
    private static async Task Beginning(Game game)
    {
        for (var i = 0; game.State.Phase == MatchPhases.TurnStart; i++)
        {
            Assert.InRange(i, 0, 7);
            if (game.State.TriggerQueue.Count > 0) await game.Act(game.State.TriggerQueue[0].ControllerId,
                new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            else if (game.State.StackItems.Count > 0) await game.Top();
            else Assert.Fail("The actual beginning has no next resolution.");
        }
    }
    private static async Task Focus(Game game)
    { for (var i = 0; game.State.FocusPlayerId is { } player; i++) { Assert.InRange(i, 0, 7); await game.Act(player, new PassFocusCommand()); } }
    private static async Task FinishBattle(Game game)
    {
        for (var i = 0; game.State.BattleState.IsActive || game.State.PendingBattleResult is not null || game.State.FocusPlayerId is not null || game.State.StackItems.Count > 0; i++)
        {
            Assert.InRange(i, 0, 19);
            if (game.State.StackItems.Count > 0) await game.Top();
            else if (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
            else if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else Assert.Fail("The actual battle has no next ordinary command.");
        }
    }
}
