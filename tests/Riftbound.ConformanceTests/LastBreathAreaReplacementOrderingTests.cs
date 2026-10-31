using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using Flow = Riftbound.ConformanceTests.LastBreathDrawBoundaryTests;

namespace Riftbound.ConformanceTests;

public sealed class LastBreathAreaReplacementOrderingTests
{
    [Theory]
    [InlineData("GUARDIAN-0")] [InlineData("GUARDIAN-1")]
    public async Task TheChosenActualGuardianFinishesBeforeTheUnreplacedDeathAndItsDrawInterruptsRemainingAreaSkills(string selectedGear)
    {
        var initial = Position(); var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        try
        {
            var funding = Enumerable.Range(0, 12).Select(i => "AREA-P1-RUNE-" + i).ToArray();
            await game.Act("P1", new TapRuneCommand(funding[0], funding));
            await game.Act("P1", new PlayCardCommand("HOST", "OGN·096/298", []));
            var hostGeneration = game.State.CardObjects["HOST"].ObjectGeneration;
            for (var i = 0; i < 2; i++)
            {
                await game.Act("P1", new PlayCardCommand("GUARDIAN-" + i, "SFD·051/221", []));
                await game.Act("P1", new RecycleRuneCommand("AREA-P1-RUNE-" + i));
                await game.Act("P1", new AssembleEquipmentCommand("GUARDIAN-" + i, "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
            }
            await game.Act("P1", new RecycleRuneCommand("AREA-P1-RUNE-11"));
            await game.Act("P1", new PlayCardCommand("UNIT", "OGN·190/298", []));
            var sourceGeneration = game.State.CardObjects["UNIT"].ObjectGeneration;
            for (var i = 0; i < 2; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("AREA-P1-RUNE-" + (i + 2)));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
            }
            Assert.Equal(1, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            // All setup commands above are ordinary accepted actions. Recover once
            // before the turn transition instead of replaying the growing journal
            // after every setup command; Act still verifies duplicate submissions.
            await game.ColdCheckpoint();
            await game.Act("P1", new EndTurnCommand());
            await game.Act("P2", new TapRuneCommand("AREA-P2-RUNE-0", ["AREA-P2-RUNE-0", "AREA-P2-RUNE-1"]));
            await game.Act("P2", new PlayCardCommand("ENEMY", "OGN·096/298", []));
            var enemyGeneration = game.State.CardObjects["ENEMY"].ObjectGeneration;
            await game.Act("P2", new EndTurnCommand());
            await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", [], ["UNIT", "HOST"]));
            await BattleWinnerPhaseBoundaryTests.PassFocus(game); await game.Act("P1", new EndTurnCommand());
            await game.Act("P2", new TapRuneCommand("AREA-P2-RUNE-0", ["AREA-P2-RUNE-0", "AREA-P2-RUNE-1"]));
            await game.Act("P2", new RecycleRuneCommand("AREA-P2-RUNE-0"));
            await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
            await game.Act("P2", new PlayCardCommand("RAY", "OGN·009/298", ["UNIT"])); await game.Top();
            // The trigger queue is the next externally visible boundary. The
            // payment and priority assertions below remain on the same journal.
            await game.ColdCheckpoint();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(3, game.State.TriggerQueue.Count);
            Assert.Equal(3, game.State.TriggerQueue.Select(t => Assert.IsType<LastBreathBattlefieldDamageContext>(t.LastBreathBattlefieldDamage).InstanceId)
                .Distinct(StringComparer.Ordinal).Count());
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            Assert.Equal(3, game.State.StackItems.Count);
            var p1Hand = game.State.PlayerZones["P1"].Hand.ToArray(); var p1Deck = game.State.PlayerZones["P1"].MainDeck.ToArray();
            var p2Hand = game.State.PlayerZones["P2"].Hand.ToArray(); var p2Deck = game.State.PlayerZones["P2"].MainDeck.ToArray();
            Assert.NotEmpty(p2Deck); var prefix = game.Events().Length;
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Equal(0, game.State.CardObjects["HOST"].Damage); Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
            Assert.Equal("P1", request.PlayerId); Assert.Equal(2, request.Options.Count);
            Assert.Equal(new[] { "ATTACHED_GEAR:HOST:GUARDIAN-0", "ATTACHED_GEAR:HOST:GUARDIAN-1" }, request.Options.Select(o => o.Id).Order());
            var choice = Assert.Single(request.Options, o => o.Id == "ATTACHED_GEAR:HOST:" + selectedGear);
            var hash = MatchStateHasher.Hash(game.State); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State));
            var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("area-wrong-guardian-controller", "P2", CommandTypes.PayCost),
                new PayCostCommand(request.Id, "RULE_REPLACEMENT", [choice.Id]), default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
            Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(rejected.State)));
            await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [choice.Id]));
            await game.ColdCheckpoint();
            Assert.Null(game.State.PendingRuleChoice);
            var firstEffects = game.Events().Skip(prefix).ToArray();
            var recalled = Assert.Single(firstEffects, e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == "HOST");
            var actualDeath = Assert.Single(firstEffects, e => e.Event.Kind == "UNIT_DESTROYED" && Target(e.Event) == "ENEMY");
            var consumed = Assert.Single(firstEffects, e => e.Event.Kind == "EQUIPMENT_DESTROYED" && Target(e.Event) == selectedGear);
            Assert.True(consumed.Sequence < recalled.Sequence && recalled.Sequence < actualDeath.Sequence);
            Assert.Equal(2, firstEffects.Count(e => e.Event.Kind == "DAMAGE_APPLIED"));
            Assert.All(firstEffects.Where(e => e.Event.Kind == "DAMAGE_APPLIED"), e => Assert.Equal(4, Convert.ToInt32(e.Event.Payload["damage"])));
            Assert.DoesNotContain(firstEffects, e => e.Event.Kind == "CARD_DRAWN" || e.Event.Kind == "UNIT_DESTROYED" && Target(e.Event) == "HOST");
            Assert.Contains("HOST", game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects["HOST"].IsExhausted);
            Assert.Equal(hostGeneration, game.State.CardObjects["HOST"].ObjectGeneration); Assert.Equal(0, game.State.CardObjects["HOST"].Damage);
            Assert.DoesNotContain(firstEffects, e => e.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
            Assert.Contains(selectedGear, game.State.PlayerZones["P1"].Graveyard);
            var otherGear = selectedGear == "GUARDIAN-0" ? "GUARDIAN-1" : "GUARDIAN-0";
            Assert.Contains(otherGear, game.State.PlayerZones["P1"].Base); Assert.Equal("HOST", game.State.CardObjects[otherGear].AttachedToObjectId);
            var cause = Assert.IsType<DestructionAttribution>(actualDeath.Event.Payload["destructionAttribution"]);
            Assert.Equal(new ObjectBinding("UNIT", sourceGeneration), cause.Source); Assert.Equal("P1", cause.ControllerId); Assert.False(cause.FromSpell);
            Assert.Equal("OGN·190/298", cause.CardNo); Assert.Equal("P1", cause.ResponsiblePlayerId);
            Assert.Equal(p1Hand, game.State.PlayerZones["P1"].Hand); Assert.Equal(p1Deck, game.State.PlayerZones["P1"].MainDeck);
            Assert.Equal(p2Hand, game.State.PlayerZones["P2"].Hand);
            if (game.State.TriggerQueue.Count > 0)
            {
                var queued = Assert.Single(game.State.TriggerQueue); Assert.Equal("P2", queued.ControllerId); Assert.Equal("ENEMY", queued.SourceObjectId);
                await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: [queued.TriggerId]));
            }
            var draw = Assert.IsType<LastBreathDrawContext>(game.State.StackItems[^1].LastBreathDraw);
            Assert.Equal(new ObjectBinding("ENEMY", enemyGeneration), draw.Source); Assert.Equal("P2", draw.ControllerId);
            Assert.Equal(3, game.State.StackItems.Count); await game.Top();
            Assert.Equal(p2Hand.Concat(p2Deck.Take(1)), game.State.PlayerZones["P2"].Hand);
            Assert.Equal(p2Deck.Skip(1), game.State.PlayerZones["P2"].MainDeck); Assert.Equal(2, game.State.StackItems.Count);
            for (var i = 0; i < 2; i++)
            {
                var birth = Assert.IsType<LastBreathBattlefieldDamageContext>(game.State.StackItems[^1].LastBreathBattlefieldDamage);
                Assert.Equal(new ObjectBinding("UNIT", sourceGeneration), birth.Source); Assert.Equal("BF", birth.BattlefieldObjectId);
                var afterDraw = game.Events().Length; await game.Top();
                Assert.DoesNotContain(game.Events().Skip(afterDraw), e => e.Event.Kind is "DAMAGE_APPLIED" or "CARD_DRAWN");
                Assert.Equal(0, game.State.CardObjects["HOST"].Damage); Assert.Equal("HOST", game.State.CardObjects[otherGear].AttachedToObjectId);
            }
            Assert.Equal(p1Hand, game.State.PlayerZones["P1"].Hand); Assert.Equal(p1Deck, game.State.PlayerZones["P1"].MainDeck);
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
            await game.ColdCheckpoint();
            var scores = game.State.PlayerScores.ToDictionary(); var closing = game.Events().Length;
            await BattleWinnerPhaseBoundaryTests.PassFocus(game);
            Assert.Single(game.Events(), e => e.Event.Kind == "BATTLE_NO_RESULT"); Assert.Single(game.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
            Assert.False(game.State.BattleState.IsActive); Assert.Equal(scores, game.State.PlayerScores);
            Assert.DoesNotContain(game.Events().Skip(closing), e => e.Event.Kind is "SCORE_GAINED" or "BATTLEFIELD_CONQUERED");
            foreach (var player in new[] { "P1", "P2" })
                Assert.Equal(12, game.State.CardObjects.Values.Count(c => c.OwnerId == player && c.Tags.Contains(CardObjectTags.RuneCard)));
            await game.Replay();
        }
        finally { await Flow.Save("area-guardian-unreplaced-death-" + selectedGear, initial, game); }
    }

    private static string? Target(GameEvent value) => value.Payload.GetValueOrDefault("targetObjectId") as string;

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position("OGN·190/298") with { RoomId = "AREA-GUARDIAN-UNREPLACED-DEATH",
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·096/298", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["ENEMY"] = new("P2", "HAND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Base = [], Hand = ["ENEMY"] } } };
        state = Flow.Add(state, "HOST", "OGN·096/298", "P1", "HAND");
        state = Flow.Add(state, "RAY", "OGN·009/298", "P2", "HAND");
        for (var i = 0; i < 2; i++)
        {
            state = Flow.Add(state, "GUARDIAN-" + i, "SFD·051/221", "P1", "HAND");
            state = Flow.Add(state, "SONG-" + i, "SFD·059/221", "P1", "BASE");
        }
        for (var i = 0; i < 12; i++)
        {
            state = Flow.Add(state, "AREA-P1-RUNE-" + i, i == 11 ? "UNL-R05" : "UNL-R02", "P1", "BASE");
            state = Flow.Add(state, "AREA-P2-RUNE-" + i, "UNL-R01", "P2", "BASE");
        }
        var reserve = Enumerable.Range(2, 10).Select(i => "AREA-P2-RUNE-" + i).ToArray();
        return state with { ObjectLocations = state.ObjectLocations.ToDictionary(p => p.Key, p => reserve.Contains(p.Key)
                ? new ObjectLocationState("P2", "RUNE_DECK") : p.Value),
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with {
                Base = state.PlayerZones["P2"].Base.Except(reserve).ToArray(), RuneDeck = reserve } } };
    }
}
